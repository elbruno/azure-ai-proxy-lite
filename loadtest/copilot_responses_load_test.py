#!/usr/bin/env python3
"""Stress test Copilot-style repository prompts through the Responses API.

The API key is read only from PROXY_API_KEY. Results never include credentials,
request context, or response content.
"""

from __future__ import annotations

import argparse
import asyncio
import json
import os
import statistics
import time
from dataclasses import asdict, dataclass
from datetime import UTC, datetime
from pathlib import Path
from typing import Any

import aiohttp


DEFAULT_CONTEXT_FILES = (
    "README.md",
    "src/AzureAIProxy/Routes/AzureOpenAI.cs",
    "src/AzureAIProxy/Services/ProxyService.cs",
)


@dataclass
class RequestResult:
    user_id: int
    status: int
    success: bool
    latency_ms: float
    time_to_first_byte_ms: float | None
    input_tokens: int | None = None
    output_tokens: int | None = None
    total_tokens: int | None = None
    error_type: str | None = None


def percentile(values: list[float], quantile: float) -> float | None:
    if not values:
        return None
    ordered = sorted(values)
    index = min(len(ordered) - 1, max(0, round((len(ordered) - 1) * quantile)))
    return ordered[index]


def load_repository_context(paths: list[str], max_chars: int) -> str:
    sections: list[str] = []
    remaining = max_chars

    for raw_path in paths:
        path = Path(raw_path)
        if not path.is_file() or remaining <= 0:
            continue

        content = path.read_text(encoding="utf-8", errors="replace")
        excerpt = content[:remaining]
        sections.append(f"\n--- FILE: {path.as_posix()} ---\n{excerpt}")
        remaining -= len(excerpt)

    if not sections:
        raise ValueError("No readable context files were provided.")

    return "".join(sections)


def extract_event_data(
    event: dict[str, Any],
) -> tuple[dict[str, Any] | None, str | None, str | None]:
    event_type = event.get("type")
    response = event.get("response")
    usage = response.get("usage") if isinstance(response, dict) else None

    if event_type == "error":
        error = event.get("error")
        if isinstance(error, dict):
            return usage, str(error.get("type") or error.get("code") or "response_error"), event_type
        return usage, "response_error", event_type

    if event_type == "response.failed" and isinstance(response, dict):
        error = response.get("error")
        if isinstance(error, dict):
            return usage, str(error.get("type") or error.get("code") or "response_failed"), event_type
        return usage, "response_failed", event_type

    if event_type == "response.incomplete":
        incomplete_details = response.get("incomplete_details") if isinstance(response, dict) else None
        reason = incomplete_details.get("reason") if isinstance(incomplete_details, dict) else None
        return usage, str(reason or "response_incomplete"), event_type

    if event_type == "response.completed":
        return usage, None, event_type

    return usage, None, None


async def run_virtual_user(
    session: aiohttp.ClientSession,
    start_event: asyncio.Event,
    endpoint: str,
    api_key: str,
    model: str,
    repository_context: str,
    max_output_tokens: int,
    reasoning_effort: str,
    user_id: int,
) -> RequestResult:
    payload = {
        "model": model,
        "instructions": (
            "You are GitHub Copilot working in a real software repository. "
            "Analyze the supplied repository context and answer concisely."
        ),
        "input": (
            f"Virtual developer {user_id} asks: identify one concrete reliability improvement "
            "for this codebase and cite the relevant file. Do not call tools.\n"
            f"{repository_context}"
        ),
        "max_output_tokens": max_output_tokens,
        "reasoning": {"effort": reasoning_effort},
        "stream": True,
    }
    headers = {
        "api-key": api_key,
        "Content-Type": "application/json",
        "Accept": "text/event-stream",
    }

    await start_event.wait()
    started = time.perf_counter()
    first_byte_at: float | None = None
    status = 0
    usage: dict[str, Any] | None = None
    error_type: str | None = None
    terminal_event: str | None = None

    try:
        async with session.post(endpoint, headers=headers, json=payload) as response:
            status = response.status

            async for raw_line in response.content:
                if first_byte_at is None and raw_line.strip():
                    first_byte_at = time.perf_counter()

                line = raw_line.decode("utf-8", errors="replace").strip()
                if not line.startswith("data:"):
                    continue

                data = line[5:].strip()
                if not data or data == "[DONE]":
                    continue

                try:
                    event = json.loads(data)
                except json.JSONDecodeError:
                    continue

                event_usage, event_error, event_terminal = extract_event_data(event)
                if event_usage:
                    usage = event_usage
                if event_error:
                    error_type = event_error
                if event_terminal:
                    terminal_event = event_terminal

            if status >= 400 and error_type is None:
                error_type = f"http_{status}"
            elif status == 200 and terminal_event != "response.completed" and error_type is None:
                error_type = "stream_ended_without_response_completed"
    except TimeoutError:
        error_type = "timeout"
    except aiohttp.ClientError:
        error_type = "client_error"

    finished = time.perf_counter()
    return RequestResult(
        user_id=user_id,
        status=status,
        success=status == 200 and terminal_event == "response.completed" and error_type is None,
        latency_ms=(finished - started) * 1000,
        time_to_first_byte_ms=(
            (first_byte_at - started) * 1000 if first_byte_at is not None else None
        ),
        input_tokens=int(usage["input_tokens"]) if usage and usage.get("input_tokens") is not None else None,
        output_tokens=int(usage["output_tokens"]) if usage and usage.get("output_tokens") is not None else None,
        total_tokens=int(usage["total_tokens"]) if usage and usage.get("total_tokens") is not None else None,
        error_type=error_type,
    )


def summarize_stage(concurrency: int, results: list[RequestResult], duration_seconds: float) -> dict[str, Any]:
    successful = [result for result in results if result.success]
    latencies = [result.latency_ms for result in successful]
    first_bytes = [
        result.time_to_first_byte_ms
        for result in successful
        if result.time_to_first_byte_ms is not None
    ]
    statuses: dict[str, int] = {}
    errors: dict[str, int] = {}

    for result in results:
        status_key = str(result.status)
        statuses[status_key] = statuses.get(status_key, 0) + 1
        if result.error_type:
            errors[result.error_type] = errors.get(result.error_type, 0) + 1

    return {
        "concurrency": concurrency,
        "duration_seconds": round(duration_seconds, 3),
        "requests": len(results),
        "successes": len(successful),
        "success_rate_percent": round((len(successful) / len(results)) * 100, 2),
        "requests_per_second": round(len(results) / duration_seconds, 3),
        "status_codes": statuses,
        "errors": errors,
        "latency_ms": {
            "mean": round(statistics.fmean(latencies), 2) if latencies else None,
            "p50": round(percentile(latencies, 0.50), 2) if latencies else None,
            "p95": round(percentile(latencies, 0.95), 2) if latencies else None,
            "p99": round(percentile(latencies, 0.99), 2) if latencies else None,
            "max": round(max(latencies), 2) if latencies else None,
        },
        "time_to_first_byte_ms": {
            "p50": round(percentile(first_bytes, 0.50), 2) if first_bytes else None,
            "p95": round(percentile(first_bytes, 0.95), 2) if first_bytes else None,
        },
        "usage": {
            "input_tokens": sum(result.input_tokens or 0 for result in successful),
            "output_tokens": sum(result.output_tokens or 0 for result in successful),
            "total_tokens": sum(result.total_tokens or 0 for result in successful),
        },
        "requests_detail": [asdict(result) for result in results],
    }


async def run_stage(
    proxy_url: str,
    api_key: str,
    model: str,
    repository_context: str,
    concurrency: int,
    max_output_tokens: int,
    reasoning_effort: str,
    timeout_seconds: int,
) -> dict[str, Any]:
    endpoint = f"{proxy_url.rstrip('/')}/responses"
    start_event = asyncio.Event()
    timeout = aiohttp.ClientTimeout(total=timeout_seconds)
    connector = aiohttp.TCPConnector(limit=concurrency, limit_per_host=concurrency)

    async with aiohttp.ClientSession(timeout=timeout, connector=connector) as session:
        tasks = [
            asyncio.create_task(
                run_virtual_user(
                    session,
                    start_event,
                    endpoint,
                    api_key,
                    model,
                    repository_context,
                    max_output_tokens,
                    reasoning_effort,
                    user_id,
                )
            )
            for user_id in range(1, concurrency + 1)
        ]
        started = time.perf_counter()
        start_event.set()
        results = await asyncio.gather(*tasks)
        duration_seconds = time.perf_counter() - started

    return summarize_stage(concurrency, results, duration_seconds)


async def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--proxy-url", required=True, help="Proxy URL ending in /api/v1")
    parser.add_argument("--model", default="gpt-5-mini")
    parser.add_argument("--concurrency", nargs="+", type=int, default=[10, 25, 50])
    parser.add_argument("--context-file", action="append", dest="context_files")
    parser.add_argument("--prompt-chars", type=int, default=32000)
    parser.add_argument("--max-output-tokens", type=int, default=4096)
    parser.add_argument(
        "--reasoning-effort",
        choices=("minimal", "low", "medium", "high"),
        default="high",
    )
    parser.add_argument("--timeout-seconds", type=int, default=180)
    parser.add_argument("--stage-pause-seconds", type=int, default=65)
    parser.add_argument(
        "--output",
        default="loadtest/copilot_responses_results.json",
    )
    args = parser.parse_args()

    api_key = os.environ.get("PROXY_API_KEY")
    if not api_key:
        raise SystemExit("Set PROXY_API_KEY in the environment.")

    context_files = args.context_files or list(DEFAULT_CONTEXT_FILES)
    repository_context = load_repository_context(context_files, args.prompt_chars)
    report: dict[str, Any] = {
        "started_at": datetime.now(UTC).isoformat(),
        "target": args.proxy_url,
        "model": args.model,
        "profile": {
            "context_files": context_files,
            "prompt_chars": len(repository_context),
            "max_output_tokens": args.max_output_tokens,
            "reasoning_effort": args.reasoning_effort,
            "stream": True,
        },
        "stages": [],
    }

    print(
        f"Copilot Responses load test: model={args.model}, "
        f"context={len(repository_context)} chars, stages={args.concurrency}"
    )

    for index, concurrency in enumerate(args.concurrency):
        print(f"\nStarting {concurrency}-user stage...")
        stage = await run_stage(
            args.proxy_url,
            api_key,
            args.model,
            repository_context,
            concurrency,
            args.max_output_tokens,
            args.reasoning_effort,
            args.timeout_seconds,
        )
        report["stages"].append(stage)
        print(
            f"  success={stage['successes']}/{stage['requests']} "
            f"({stage['success_rate_percent']}%), "
            f"p95={stage['latency_ms']['p95']}ms, "
            f"429={stage['status_codes'].get('429', 0)}"
        )

        if index < len(args.concurrency) - 1:
            print(f"  Waiting {args.stage_pause_seconds}s for the quota window...")
            await asyncio.sleep(args.stage_pause_seconds)

    report["completed_at"] = datetime.now(UTC).isoformat()
    output_path = Path(args.output)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(f"\nResults written to {output_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(asyncio.run(main()))
