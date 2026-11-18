# 0004. No vector search

## Context

The first design stored embeddings in pgvector to retrieve resume passages for each requirement.

## Decision

The extension was removed before any code used it. A resume and a profile's facts fit in a model's context whole, so there is nothing to retrieve.

## Consequences

One less moving part, and the model sees every line rather than a retrieved subset, which keeps citations complete. If documents grow beyond a context window, retrieval would have to come back.
