
import * as feedback from './feedback';
import * as apiClient from '../api/apiClient';
import { API } from '../config/constants';

jest.mock('../api/apiClient', () => ({
  ...jest.requireActual('../api/apiClient'),
  apiPost: jest.fn(),
}));

const mockApiPost = apiClient.apiPost as jest.Mock;

describe('feedback service', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('submitFeedback', () => {
    it('should submit feedback and return feedbackId', async () => {
      const mockResponse = { feedbackId: 'fb123' };
      mockApiPost.mockResolvedValue(mockResponse);

      const result = await feedback.submitFeedback('Great app!', 'dummy-token');

      expect(apiClient.apiPost).toHaveBeenCalledWith(API.FEEDBACK, { message: 'Great app!' });
      expect(result.feedbackId).toBe('fb123');
    });

    it('should not include token in request body', async () => {
      mockApiPost.mockResolvedValue({ feedbackId: 'fb456' });

      await feedback.submitFeedback('Hello', 'secret-token');

      const callArgs = mockApiPost.mock.calls[0][1];
      expect(callArgs).toEqual({ message: 'Hello' });
      expect(callArgs).not.toHaveProperty('token');
    });

    it('should handle API error', async () => {
      mockApiPost.mockRejectedValue(new apiClient.ApiError(400, 'Invalid feedback'));

      await expect(feedback.submitFeedback('', 'dummy-token')).rejects.toThrow('Invalid feedback');
    });
  });
});

