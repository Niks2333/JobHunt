import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { ApplicationAnalyticsResponse } from '../models/application-analytics-response.model';
import { GetApplicationAnalyticsRequest } from '../models/get-application-analytics-request.model';

@Injectable({ providedIn: 'root' })
export class ApplicationAnalyticsService {
  private readonly httpClient = inject(HttpClient);

  getApplicationAnalytics(request: GetApplicationAnalyticsRequest) {
    let parameters = new HttpParams();
    if (request.startDate) parameters = parameters.set('startDate', request.startDate);
    if (request.endDate) parameters = parameters.set('endDate', request.endDate);
    return this.httpClient.get<ApplicationAnalyticsResponse>('/api/analytics/applications', {
      params: parameters,
    });
  }
}
