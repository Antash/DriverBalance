import { Link } from 'react-router-dom'

/** What a hosted DriverBalance stores and who can see it. Plain text on purpose: it must match what the server really does. */
export default function Privacy() {
  return (
    <main className="main">
      <div className="card">
        <h1>Privacy</h1>
        <h3>What is stored</h3>
        <p>
          Your Google account id, email address, name and picture (to sign you in), and everything you add or import: cars,
          trips (including their start and end addresses from the export), expenses, maintenance, tyres, documents you upload
          and the API tokens you create (only a hash of each token).
        </p>
        <h3>Why</h3>
        <p>Only to show you your own statistics. Nothing is sold, shared or used for advertising.</p>
        <h3>Who can see it</h3>
        <p>
          You. Other users cannot see or change your data. The owner who runs this service can technically reach the database and
          the files, because they operate it, but does not look at your data.
        </p>
        <h3>Your choices</h3>
        <p>
          You can download all your data and delete your account (with everything in it) any time on the <b>Account</b> page.
          Deleting removes your cars, trips, expenses, documents and invite; you can only come back if you are invited again.
        </p>
        <p className="muted"><Link to="/">Back</Link></p>
      </div>
    </main>
  )
}
