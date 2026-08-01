import { render, screen } from '@testing-library/react';
import AuditLookup from './AuditLookup.jsx';

test('renders audit lookup inputs', () => {
  render(<AuditLookup />);
  expect(screen.getByText(/Audit Lookup/i)).toBeInTheDocument();
});
