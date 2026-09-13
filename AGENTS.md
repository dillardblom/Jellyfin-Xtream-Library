# Jellyfin Xtream Library

Project context for this repository lives in **[CLAUDE.md](CLAUDE.md)**: structure, build and test
commands, the release process, the manifest conventions, and the code analysis rules. Read that
file.

This file exists only because some tools look for `AGENTS.md` and not for `CLAUDE.md`. It is a
pointer on purpose. It used to be a copy, and the copy went stale: it still described building the
release zip by hand and uploading it with `gh release create`, months after CI took that over and
started deriving the version from the tag. An agent following it would have released the wrong way.
One file is the source of truth, and it is not this one.

Local-only notes that are not in git live in `CLAUDE.local.md`.
