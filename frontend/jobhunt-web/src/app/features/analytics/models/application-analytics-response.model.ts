export interface ApplicationBreakdownResponse {
  label: string;
  count: number;
}

export interface ApplicationAnalyticsResponse {
  startDate: string | null;
  endDate: string | null;
  currentMonthStartDate: string;
  totalApplications: number;
  applicationsThisMonth: number;
  applicationsReachingInterview: number;
  interviewConversionPercentage: number;
  applicationsByStatus: ApplicationBreakdownResponse[];
  applicationsBySource: ApplicationBreakdownResponse[];
}
