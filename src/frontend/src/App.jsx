import ConversionForm from './components/ConversionForm.jsx';
import AuditLookup from './components/AuditLookup.jsx';

export default function App() {
  return (
    <div style={{ padding: 16, fontFamily: 'system-ui, -apple-system, Segoe UI, Roboto, Arial' }}>
      <h1 style={{ marginTop: 0 }}>Real-Time Currency Conversion</h1>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr', gap: 16 }}>
        <div>
          <ConversionForm />
        </div>
        <div>
          <AuditLookup />
        </div>
      </div>
    </div>
  );
}
