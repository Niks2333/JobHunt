import { JobApplicationStatus } from './job-application-response.model';

export interface JobApplicationStatusHistoryResponse {
  jobApplicationStatusHistoryId: number;
  status: JobApplicationStatus;
  occurredOn: string;
  recordedAtUtc: string;
}
