# Repository documentation rules

- The repository root must contain no documentation files except `README.md` and the license file.
- Put all other project documentation under `docs/` in the most relevant existing section.
- Keep published user and administrator guides under `docs/docs/` and add them to
  `docs/mkdocs.yml` when they belong in the public site.
- Keep component-specific README files beside their component only when they are necessary for
  working with that component.
- Before moving or deleting documentation, update every repository-relative link, workflow
  reference, and generated UI link that points to it.
- Do not duplicate a canonical guide at the repository root. Merge useful content into the
  canonical page under `docs/docs/`.
- Keep private, machine-specific deployment notes under `docs/private/` and exclude them from Git.
