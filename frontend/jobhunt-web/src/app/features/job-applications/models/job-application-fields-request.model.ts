export interface JobApplicationFieldsRequest {
  companyName: string;
  roleTitle: string;
  location: string | null;
  source: string | null;
  jobUrl: string | null;
  applicationDate: string;
  notes: string | null;
}
