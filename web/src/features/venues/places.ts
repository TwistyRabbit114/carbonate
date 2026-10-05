import type { Venue } from '@/api/types';
import { formatClock } from '@/lib/format';

//a search on the address, opened in whatever maps app the phone has
export const mapLink = (address: string) => `https://maps.google.com/?q=${encodeURIComponent(address)}`;

//"07:00 to 23:00". the end can be earlier than the start, for a venue open past midnight
export function openingHours(venue: Venue) {
  if (!venue.operatingHoursStart || !venue.operatingHoursEnd) return undefined;
  return `${formatClock(venue.operatingHoursStart)} to ${formatClock(venue.operatingHoursEnd)}`;
}
