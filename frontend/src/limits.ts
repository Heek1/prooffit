// Mirrors backend/src/ProofFit.Api/Domain/Limits.cs so the form can warn before sending.
// The server enforces these; the copies here only make the feedback faster.
export const RESUME_FILE_BYTES = 5 * 1024 * 1024
export const VACANCY_TITLE_LENGTH = 200
export const VACANCY_TEXT_MIN_LENGTH = 50
export const VACANCY_TEXT_MAX_LENGTH = 20_000
