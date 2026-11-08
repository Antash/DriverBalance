import { useParams } from '../state/ParamsContext'

/** Trip lists are grouped by the saved parameters; say so while the user is trying unsaved changes. */
export function SavedParamsNote() {
  const { dirty } = useParams()
  if (!dirty) return null
  return <p className="warn" role="note">Grouping uses saved parameters — save to apply your trial changes here.</p>
}
