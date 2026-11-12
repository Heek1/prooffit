import { useState, type DragEvent } from 'react'
import { ApiError, uploadResume, type Resume } from '../api.ts'
import { RESUME_FILE_BYTES } from '../limits.ts'
import { DocumentText } from './DocumentText.tsx'
import { ErrorList, Sheet, TextButton } from './Sheet.tsx'

type ResumeSheetProps = {
  resume: Resume | null
  onChange: (resume: Resume | null) => void
}

export function ResumeSheet({ resume, onChange }: ResumeSheetProps) {
  const [uploading, setUploading] = useState(false)
  const [dragging, setDragging] = useState(false)
  const [errors, setErrors] = useState<string[]>([])

  async function upload(file: File | undefined) {
    if (!file || uploading) return
    if (file.size > RESUME_FILE_BYTES) {
      setErrors([`The file is larger than ${RESUME_FILE_BYTES / (1024 * 1024)} MB.`])
      return
    }

    setUploading(true)
    setErrors([])
    try {
      onChange(await uploadResume(file))
    } catch (error) {
      setErrors(error instanceof ApiError ? error.messages : ['The upload failed. Try again.'])
    } finally {
      setUploading(false)
    }
  }

  function drop(event: DragEvent) {
    event.preventDefault()
    setDragging(false)
    void upload(event.dataTransfer.files[0])
  }

  if (resume) {
    const pages = resume.pageCount === 1 ? '1 page' : `${resume.pageCount} pages`
    return (
      <Sheet
        title="Resume"
        detail={`${resume.fileName}, ${pages}`}
        action={<TextButton onClick={() => onChange(null)}>Replace</TextButton>}
      >
        <DocumentText text={resume.text} label="Resume text" />
      </Sheet>
    )
  }

  return (
    <Sheet title="Resume">
      <label
        onDragOver={(event) => {
          event.preventDefault()
          setDragging(true)
        }}
        onDragLeave={() => setDragging(false)}
        onDrop={drop}
        className={`dropzone flex min-h-72 cursor-pointer flex-col items-center justify-center gap-2 rounded-sm border-2 border-dashed px-6 py-10 text-center ${
          dragging ? 'border-ink bg-marker/40' : 'border-rule hover:border-ink'
        }`}
      >
        <input
          type="file"
          accept="application/pdf,.pdf"
          className="sr-only"
          disabled={uploading}
          onChange={(event) => {
            void upload(event.target.files?.[0])
            // Clearing the value lets the same file be picked again after an error.
            event.target.value = ''
          }}
        />
        <span className="text-lg font-medium">
          {uploading ? 'Reading your resume…' : 'Choose a PDF resume'}
        </span>
        <span className="text-sm text-ink-soft">
          or drop it here. Up to {RESUME_FILE_BYTES / (1024 * 1024)} MB, with selectable text.
        </span>
      </label>
      <ErrorList messages={errors} />
    </Sheet>
  )
}
