import { render, screen } from '@testing-library/react';
import ConversionForm from './ConversionForm.jsx';

test('renders conversion form inputs', () => {
  render(<ConversionForm />);
  expect(screen.getByText(/New Conversion/i)).toBeInTheDocument();
});
