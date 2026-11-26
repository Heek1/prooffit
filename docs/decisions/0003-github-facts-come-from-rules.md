# 0003. GitHub evidence is gathered by fixed rules, not by a model

## Context

A candidate's public code can prove what a resume leaves out, but whatever is shown as a fact must be checkable by opening the repository.

## Decision

`RepositorySignals` detects skills from file paths and dependency manifests (`package.json`, `*.csproj`, Python requirements). Forks, vendored code and build output are ignored, and one stray file does not claim a language. A profile is cached for a day.

## Consequences

Every fact names the repositories it came from. The rules see less than a model reading the code would, and need extending as new technologies appear. Without a token GitHub allows 60 requests an hour, so the server uses a read-only token.
