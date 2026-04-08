// @ts-nocheck
import { apiPost } from '../api/apiClient';
import { API } from '../config/constants';

export interface FeedbackResponse {
  feedbackId: string;
}

export async function submitFeedback(message: string, _token: string): Promise<FeedbackResponse> {
  return apiPost<FeedbackResponse>(API.FEEDBACK, { message });
}
