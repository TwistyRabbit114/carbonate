import type { MfaStep, SignedInSession } from '@/api/types';

//the two session bodies the auth endpoints send back, with every field present as the api sends them

export function signedInSession(accessToken = 'test-token'): SignedInSession {
  return {
    mfaRequired: false,
    mfaEnrolmentRequired: false,
    mfaToken: null,
    accessToken,
    expiresInSeconds: 900,
  };
}

export function mfaStep(mfaToken: string, enrolmentRequired = false): MfaStep {
  return {
    mfaRequired: true,
    mfaEnrolmentRequired: enrolmentRequired,
    mfaToken,
    accessToken: null,
    expiresInSeconds: null,
  };
}
