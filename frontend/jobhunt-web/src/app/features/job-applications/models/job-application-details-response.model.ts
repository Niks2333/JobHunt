import { JobApplicationResponse } from './job-application-response.model';
import { JobApplicationStatusHistoryResponse } from './job-application-status-history-response.model';

export interface JobApplicationDetailsResponse {
  application: JobApplicationResponse;
  statusHistory: JobApplicationStatusHistoryResponse[];
}
