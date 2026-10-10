import { JobApplicationStatus } from './job-application-response.model';

export interface GetJobApplicationsRequest {
  search: string;
  status: JobApplicationStatus | '';
  source: string;
  pageNumber: number;
  pageSize: number;
}
