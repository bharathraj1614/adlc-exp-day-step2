import { useState } from 'react';
import { postJson } from '../lib/api';

export default function ConversionForm() {
  const [amount, setAmount] = useState('');
  const [fromCurrency, setFromCurrency] = useState('USD');
  const [toCurrency, setToCurrency] = useState('EUR');
  const [result, setResult] = useState(null);
  const [error, setError] = useState(null);
  const [submitting, setSubmitting] = useState(false);

  async function onSubmit(e) {
    e.preventDefault();
    setError(null);
    setResult(null);

    const parsedAmount = Number(amount);
    const from = (fromCurrency || '').toUpperCase();
    const to = (toCurrency || '').toUpperCase();

    const currencyRegex = /^[A-Z]{3}$/;
    if (!(parsedAmount > 0)) {
      setError({ title: 'Validation error', detail: 'amount must be greater than 0.' });
      return;
    }
    if (!currencyRegex.test(from) || !currencyRegex.test(to)) {
      setError({ title: 'Validation error', detail: 'Currency codes must be 3-letter uppercase ISO codes.' });
      return;
    }

    setSubmitting(true);
    try {
      const payload = await postJson('/api/conversions', {
        amount: parsedAmount,
        fromCurrency: from,
        toCurrency: to,
      });
      setResult(payload);
    } catch (e2) {
      const problem = e2 && typeof e2 === 'object' ? e2 : { detail: String(e2) };
      setError({ title: problem.title || 'Request failed', detail: problem.detail || 'Unable to complete conversion.' });
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <section style={{ border: '1px solid #ddd', borderRadius: 8, padding: 12 }}>
      <h2 style={{ marginTop: 0 }}>New Conversion</h2>

      <form onSubmit={onSubmit} style={{ display: 'grid', gridTemplateColumns: '1fr', gap: 10 }}>
        <label>
          Amount
          <input
            value={amount}
            onChange={(e) => setAmount(e.target.value)}
            inputMode="decimal"
            style={{ width: '100%', padding: 8, marginTop: 4 }}
            placeholder="100.00"
          />
        </label>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
          <label>
            From
            <input
              value={fromCurrency}
              onChange={(e) => setFromCurrency(e.target.value)}
              style={{ width: '100%', padding: 8, marginTop: 4 }}
              placeholder="USD"
            />
          </label>
          <label>
            To
            <input
              value={toCurrency}
              onChange={(e) => setToCurrency(e.target.value)}
              style={{ width: '100%', padding: 8, marginTop: 4 }}
              placeholder="EUR"
            />
          </label>
        </div>

        <button type="submit" disabled={submitting} style={{ padding: 10, cursor: 'pointer' }}>
          {submitting ? 'Converting…' : 'Convert'}
        </button>
      </form>

      {error ? (
        <div style={{ marginTop: 10, color: '#b00020' }}>
          <strong>{error.title}</strong>
          <div>{error.detail}</div>
        </div>
      ) : null}

      {result ? (
        <div style={{ marginTop: 10 }}>
          <h3 style={{ marginBottom: 6 }}>Result</h3>
          <div>Converted Amount: <strong>{result.convertedAmount}</strong> {result.toCurrency}</div>
          <div>Rate: <strong>{result.rate}</strong></div>
          <div>Conversion ID: <code>{result.conversionId}</code></div>
          <div>Executed At (UTC): <code>{result.executedAtUtc}</code></div>
        </div>
      ) : null}
    </section>
  );
}
