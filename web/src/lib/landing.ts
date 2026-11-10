/**
 * The words of the start page people see before they sign in. index.html carries a static copy of the same text (for
 * visitors and scanners that do not run scripts); a test keeps the two in step.
 */
export const LANDING = {
  title: 'Know how you drive and what your car really costs',
  intro:
    'DriverBalance turns the driving journal you export from the Volvo Cars app into clear statistics: trips, fuel use, ' +
    'DPF health, oil intervals, maintenance and the full cost of owning your car.',
  signInNote:
    'Invite-only for now. Google sign-in is used only to recognise you: DriverBalance never sees your Google password.',
  features: [
    { title: 'Trips and habits', text: 'Distance, driving time, when you drive, your longest journeys and a driving calendar.' },
    { title: 'Fuel', text: 'Consumption per trip and over time, fuel burned per active day, cold starts and short trips.' },
    { title: 'DPF health', text: 'For diesels: whether the particulate filter gets the long drives it needs, with a reminder when it is overdue.' },
    { title: 'Oil and maintenance', text: 'Every service visit with the work and parts, oil change intervals side by side, tyre sets and swaps.' },
    { title: 'Costs', text: 'Fuel, service, insurance, tolls and more, cost per km, and purchase, financing and resale in one cost of ownership.' },
    { title: 'Your data stays yours', text: 'Only you see your data. Download it or delete your account at any time. No ads, nothing sold.' },
  ],
  steps: [
    'Export the driving journal (CSV) from the Volvo Cars app.',
    'Import it into DriverBalance.',
    'Add fuel, service and other costs as they happen. Not ready yet? Try a demo car with made-up data first.',
  ],
  about: 'DriverBalance is a personal, non-commercial project. It is not affiliated with or endorsed by Volvo Cars.',
} as const
