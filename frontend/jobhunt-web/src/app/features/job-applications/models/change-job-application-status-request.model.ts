import { JobApplicationStatus } from './job-application-response.model';

export interface ChangeJobApplicationStatusRequest {
  status: JobApplicationStatus;
  occurredOn: string;
}
