export type JobApplicationStatus =
  'Applied' | 'Assessment' | 'Interview' | 'Offer' | 'Rejected' | 'Withdrawn';

export interface JobApplicationResponse {
  jobApplicationId: number;
  companyName: string;
  roleTitle: string;
  location: string | null;
  source: string | null;
  jobUrl: string | null;
  applicationDate: string;
  status: JobApplicationStatus;
  notes: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}
