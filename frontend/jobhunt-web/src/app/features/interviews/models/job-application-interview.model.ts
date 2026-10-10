export type InterviewMode = 'Online' | 'Phone' | 'InPerson';

export interface SaveJobApplicationInterviewRequest {
  roundName: string;
  scheduledAt: string;
  mode: InterviewMode;
  meetingUrl: string | null;
  location: string | null;
  preparationNotes: string | null;
  feedback: string | null;
  isCancelled: boolean;
}

export interface JobApplicationInterviewResponse {
  jobApplicationInterviewId: number;
  jobApplicationId: number;
  roundName: string;
  scheduledAtUtc: string;
  mode: InterviewMode;
  meetingUrl: string | null;
  location: string | null;
  preparationNotes: string | null;
  feedback: string | null;
  isCancelled: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface UpcomingInterviewsResponse {
  items: { interview: JobApplicationInterviewResponse; companyName: string; roleTitle: string }[];
  totalUpcomingInterviews: number;
}
