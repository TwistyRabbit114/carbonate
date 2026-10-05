//the run of the night stored on the event (FR-05), in the shape of the service schedule json
//schema. it arrives as plain json, so it's checked here before anything renders it, and a
//schedule that doesn't fit is treated as missing rather than half shown

//----------------------------------------------------------\
//                              TYPES
//----------------------------------------------------------\

export const expectedLoads = ['low', 'moderate', 'peak', 'table-service'] as const;
export type ExpectedLoad = (typeof expectedLoads)[number];

export type ServiceWindow = {
  label: string;
  start: string; //clock times in SAST, "18:30"
  end: string;
  expectedLoad: ExpectedLoad;
  barsOpen?: number;
  staffOnShift?: number;
};

export type ServiceSchedule = {
  doorsOpen: string;
  doorsClose?: string;
  dinnerServiceAt?: string;
  serviceWindows: ServiceWindow[];
  layoutNotes?: string;
};

export const loadLabels: Record<ExpectedLoad, string> = {
  low: 'Quiet',
  moderate: 'Steady',
  peak: 'Peak',
  'table-service': 'Table service',
};

//----------------------------------------------------------\
//                              READING
//----------------------------------------------------------\

const clock = /^\d{2}:\d{2}(:\d{2})?$/;

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null && !Array.isArray(value);

const optionalClock = (value: unknown) =>
  typeof value === 'string' && clock.test(value) ? value : undefined;
const optionalCount = (value: unknown) =>
  typeof value === 'number' && Number.isInteger(value) && value >= 0 ? value : undefined;

function readWindow(value: unknown): ServiceWindow | null {
  if (!isRecord(value)) return null;
  const { label, start, end, expectedLoad } = value;
  if (typeof label !== 'string' || !optionalClock(start) || !optionalClock(end)) return null;
  if (!expectedLoads.includes(expectedLoad as ExpectedLoad)) return null;
  return {
    label,
    start: start as string,
    end: end as string,
    expectedLoad: expectedLoad as ExpectedLoad,
    barsOpen: optionalCount(value.barsOpen),
    staffOnShift: optionalCount(value.staffOnShift),
  };
}

export function readServiceSchedule(value: unknown): ServiceSchedule | null {
  if (!isRecord(value) || !optionalClock(value.doorsOpen) || !Array.isArray(value.serviceWindows))
    return null;
  const windows = value.serviceWindows.map(readWindow);
  if (windows.length === 0 || windows.some((window) => window === null)) return null;

  return {
    doorsOpen: value.doorsOpen as string,
    doorsClose: optionalClock(value.doorsClose),
    dinnerServiceAt: optionalClock(value.dinnerServiceAt),
    serviceWindows: windows as ServiceWindow[],
    layoutNotes:
      typeof value.layoutNotes === 'string' && value.layoutNotes.trim() ? value.layoutNotes : undefined,
  };
}
