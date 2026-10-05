//----------------------------------------------------------\\
//                              PROBLEM DETAILS
//----------------------------------------------------------\\

//rfc 7807 body the api returns on errors. asp.net writes extensions as top-level
//properties, so the plan's "extensions.current" arrives here as problem.current
export type Problem = {
  type?: string;
  title?: string;
  status: number;
  detail?: string;
  errors?: Record<string, string[]>; //400 validation, keyed by field
  [extension: string]: unknown;
};

//----------------------------------------------------------\\
//                              API ERROR
//----------------------------------------------------------\\

//no constructor parameter properties, erasableSyntaxOnly is on
export class ApiError extends Error {
  readonly problem: Problem;

  constructor(problem: Problem) {
    super(problem.title ?? `Request failed (${problem.status})`);
    this.name = 'ApiError';
    this.problem = problem;
  }

  get status() {
    return this.problem.status;
  }

  static async fromResponse(res: Response) {
    let body: Partial<Problem> = {};
    if (res.headers.get('content-type')?.includes('json')) {
      try {
        body = (await res.json()) as Partial<Problem>;
      } catch {
        //a broken body still leaves us the status code
      }
    }
    return new ApiError({ ...body, status: res.status });
  }

  //status 0 means the request never got an answer: offline, dns, or the api is down
  static network() {
    return new ApiError({ status: 0, title: 'Network unavailable' });
  }
}

export function isApiError(error: unknown): error is ApiError {
  return error instanceof ApiError;
}
