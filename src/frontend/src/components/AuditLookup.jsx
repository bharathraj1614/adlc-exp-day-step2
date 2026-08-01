import { useState } from 'react';
import { getJson } from '../lib/api';

export default function AuditLookup() {
  const [conversionId, setConversionId] = useState('');
  const [result, setResult] = useState(null);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(false);

  async function onLookup(e) {
    e.preventDefault();
    setError(null);
    setResult(null);

    if (!conversionId || conversionId.trim().length < 10) {
      setError({ title: 'Validation error', detail: 'Enter a conversionId GUID.' });
      return;
    }

    setLoading(true);
    try {
      const payload = await getJson(`/api/conversions/${conversionId.trim()}`);
      setResult(payload);
    } catch (e2) {
      const problem = e2 && typeof e2 === 'object' ? e2 : { detail: String(e2) };
      setError({ title: problem.title || 'Lookup failed', detail: problem.detail || 'No audit record found.' });
    } finally {
      setLoading(false);
    }
  }

  return (
    <section style={{ border: '1px solid #ddd', borderRadius: 8, padding: 12 }}>
      <h2 style={{ marginTop: 0 }}>Audit Lookup</h2>
      <form onSubmit={onLookup} style={{ display: 'grid', gridTemplateColumns: '1fr', gap: 10 }}>
        <label>
          Conversion ID
          <input
            value={conversionId}
            onChange={(e) => setConversionId(e.target.value)}
            style={{ width: '100%', padding: 8, marginTop: 4 }}
            placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
          />
        </label>
        <button type="submit" disabled={loading} style={{ padding: 10, cursor: 'pointer' }}>
          {loading ? 'Looking up…' : 'Lookup'}
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
          <h3 style={{ marginBottom: 6 }}>Audit Record</h3>
          <div>Amount: <strong>{result.amount}</strong> {result.fromCurrency}</div>
          <div>Converted: <strong>{result.convertedAmount}</strong> {result.toCurrency}</div>
          <div>Rate: <strong>{result.rate}</strong></div>
          <div>Provider Date: <code>{result.providerDate ?? ''}</code></div>
          <div>Provider Sequence Marker: <code>{result.providerSequenceMarker ?? ''}</code></div>
          <div>Executed At (UTC): <code>{result.executedAtUtc}</code></div>
        </div>
      ) : null}
    </section>
  );
}
