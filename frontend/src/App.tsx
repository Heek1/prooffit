import { BrowserRouter, Route, Routes } from 'react-router'
import { ComparePage } from './pages/ComparePage.tsx'
import { ReportPage } from './pages/ReportPage.tsx'

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<ComparePage />} />
        <Route path="/analyses/:id" element={<ReportPage />} />
        <Route path="*" element={<ComparePage />} />
      </Routes>
    </BrowserRouter>
  )
}
