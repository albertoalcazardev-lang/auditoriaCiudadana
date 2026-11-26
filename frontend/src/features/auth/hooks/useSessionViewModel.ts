import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { HttpClientError } from '../../../core/api/httpClient';
import { restoreSession, signOut } from '../api/authApi';

export const sessionKey = ['auth', 'me'] as const;

export function useSessionViewModel() {
  const client = useQueryClient();
  const session = useQuery({
    queryKey: sessionKey, queryFn: ({ signal }) => restoreSession(signal), retry: false,
    refetchOnWindowFocus: true,
  });
  const logout = useMutation({
    mutationFn: signOut, retry: false,
    onSuccess: async () => {
      await client.cancelQueries({ queryKey: sessionKey });
      client.setQueryData(sessionKey, null);
    },
    onError: (error) => {
      if (error instanceof HttpClientError && error.statusCode === 401)
        void client.invalidateQueries({ queryKey: sessionKey });
    },
  });
  return { session, logout };
}
