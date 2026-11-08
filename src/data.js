export const LOAD = {
  pickup: {
    name: 'Northgate Logistics',
    address: '4800 Industrial Blvd',
    city: 'Dallas, TX',
    appt: '07:30',
    door: 'Gate 1 · Door 14'
  },
  delivery: {
    name: 'Gulf Coast Distribution',
    address: '9200 Harbor Road',
    city: 'Houston, TX',
    appt: '14:00',
    door: 'Dock 14'
  },
  miles: 240,
  rate: 2450,
  weight: '38,500 lb',
  equipment: "53' dry container",
  seal: '482913',
  commodity: 'Palletized retail',
  broker: 'LoadLink · Mike R.'
};

export const STAGES = [
  { key: 'call', name: 'The Call', kicker: 'SHIFT 01 · 06:12', clock: '06:12', mode: 'call' },
  { key: 'load', name: 'Load Details', kicker: 'SHIFT 01 · 06:18', clock: '06:18', mode: 'call' },
  { key: 'pretrip', name: 'Walk-Around', kicker: 'SHIFT 01 · 06:24', clock: '06:24', mode: 'pretrip' },
  { key: 'gate', name: 'Gate Check-In', kicker: 'SHIFT 01 · 07:22', clock: '07:22', mode: 'gate' },
  { key: 'couple', name: 'Hook Up', kicker: 'SHIFT 01 · 07:52', clock: '07:52', mode: 'couple' },
  { key: 'seal', name: 'Seal & Send', kicker: 'SHIFT 01 · 08:06', clock: '08:06', mode: 'seal' },
  { key: 'road', name: 'On the Road', kicker: 'SHIFT 01 · 08:20', clock: '08:20', mode: 'road' },
  { key: 'delivery', name: 'Back It In', kicker: 'SHIFT 01 · 13:42', clock: '13:42', mode: 'delivery' },
  { key: 'pod', name: 'POD', kicker: 'SHIFT 01 · 13:58', clock: '13:58', mode: 'pod' },
  { key: 'debrief', name: 'Shift Debrief', kicker: 'SHIFT 01 · 14:10', clock: '14:10', mode: 'debrief' }
];

export const CALL_LINES = [
  "Morning, driver. It's Mike. You awake? I've got a loaded fifty-three footer out of Northgate — good paying run.",
  "Pickup is Northgate Logistics, 4800 Industrial Boulevard, Dallas. Appointment is seven-thirty. They charge for no-shows, so don't be late.",
  "Delivery is Gulf Coast Distribution, 9200 Harbor Road, Houston. Appointment is fourteen hundred. It's about two hundred forty miles.",
  "Fifty-three foot dry container, thirty-eight thousand five hundred pounds, palletized retail. Rate is twenty-four fifty all-in. Seal number is four-eight-two-nine-one-three.",
  "I'm sending the load screenshot to your TeleLink now. Open your load app, verify the appointment and the seal, then call me if anything doesn't match. Wake up and get rolling."
];

export const QUIZ = [
  {
    q: 'What is the pickup appointment?',
    options: ['06:30', '07:30', '08:00'],
    answer: 1,
    explain: 'Northgate Logistics, 07:30. The gate will turn you away if you miss the appointment window.'
  },
  {
    q: 'Which seal number is on the load?',
    options: ['482193', '482913', '489213'],
    answer: 1,
    explain: 'Seal 482913. Photograph the number before you leave the shipper — a mismatched seal is a red flag.'
  },
  {
    q: 'What is the all-in load rate?',
    options: ['$2,150', '$2,450', '$2,540'],
    answer: 1,
    explain: '$2,450 all-in for 240 miles. Confirm the rate on the load app before you accept.'
  }
];

export const INSPECTIONS = [
  {
    id: 'tires',
    label: 'Tires',
    title: 'Tires & wheels',
    body: 'Walk the tractor. Look for cuts, bulges, exposed cord, low tread, and loose lug nuts. Check the valve caps and listen for air leaks.',
    tip: 'A steer-tire failure at highway speed is a life-threatening event. Never skip this check.',
    pos: [1.35, 0.75, 10.95],
    cam: [[4.8, 1.8, 13.8], [1.2, 1.0, 10.9]]
  },
  {
    id: 'lights',
    label: 'Lights',
    title: 'Lights & reflectors',
    body: 'Check headlights, turn signals, four-ways, brake lights, marker lights, and reflectors. Wipe the lenses and confirm every lamp works.',
    tip: 'A burned-out marker light can put you out of service at a roadside inspection.',
    pos: [0.85, 1.35, 12.35],
    cam: [[3.4, 2.6, 16.2], [0.5, 1.4, 12.3]]
  },
  {
    id: 'fifth',
    label: 'Fifth wheel',
    title: 'Fifth wheel',
    body: 'Inspect the plate, jaws, and release handle. The jaws must be closed around the kingpin. Look for cracks, missing bolts, and excessive grease.',
    tip: 'A high hook is one of the most common causes of a dropped trailer. Always tug-test after coupling.',
    pos: [0.0, 1.25, 6.15],
    cam: [[4.6, 2.7, 8.8], [0.0, 1.15, 6.1]]
  },
  {
    id: 'air',
    label: 'Air lines',
    title: 'Air lines & gladhands',
    body: 'Check the red and blue air lines for cracks, chafing, and missing seals. Gladhands should lock securely. Drain the air tanks and listen for leaks.',
    tip: 'A leaking gladhand can release the trailer brakes or leave you without stopping power.',
    pos: [0.95, 1.0, 6.6],
    cam: [[4.2, 2.4, 9.4], [0.85, 1.0, 6.5]]
  },
  {
    id: 'flaps',
    label: 'Mud flaps',
    title: 'Mud flaps & frame',
    body: 'Check mud flaps, splash guards, frame rails, crossmembers, and the rear underride guard. Look for cracks, missing fasteners, and dragging parts.',
    tip: 'A dragging mud flap can be cited, and a cracked frame rail is a serious structural defect.',
    pos: [-1.15, 0.6, 4.85],
    cam: [[-4.4, 1.9, 7.6], [-1.1, 0.6, 4.8]]
  }
];

export const COUPLE_STEPS = [
  {
    title: 'Tug test',
    text: 'Pull forward gently against the locked fifth wheel. The trailer should not move independently. If it does, stop and re-couple.',
    button: 'Run tug test'
  },
  {
    title: 'Raise the landing gear',
    text: 'Crank the landing gear fully up and secure the handle. The trailer weight is now on the fifth wheel, not the legs.',
    button: 'Raise landing gear'
  },
  {
    title: 'Connect the air lines',
    text: 'Connect the red and blue gladhands, then open the trailer air supply. Listen for a positive lock and check for leaks.',
    button: 'Connect air lines'
  },
  {
    title: 'Trailer brake test',
    text: 'Set the trailer parking brake, release the tractor brake, and tug gently. The trailer should hold. Then release and confirm the brakes release.',
    button: 'Test trailer brakes'
  }
];

export const TIPS = {
  safety: 'Safety score is the one that follows you home. Slow is smooth, and smooth is fast around a trailer.',
  compliance: 'Match the paperwork to the load before you move. Appointment, seal, weight, and destination must agree.',
  communication: 'Send dispatch a clear photo the first time. A readable seal number prevents a phone call later.',
  time: 'You do not get paid for rushing. You get paid for a clean, on-time delivery.'
};
