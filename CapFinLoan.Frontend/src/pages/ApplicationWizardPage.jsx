import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { createApplication, updateApplication, submitApplication } from '../services/applicationService';

const STEPS = ['Personal Details', 'Employment', 'Loan Details', 'Review'];

const EMPTY = {
  personalDetails: {
    firstName: '', lastName: '', dateOfBirth: '', gender: '',
    email: '', phone: '', addressLine1: '', addressLine2: '',
    city: '', state: '', postalCode: '',
  },
  employmentDetails: {
    employerName: '', employmentType: 'Salaried',
    monthlyIncome: '', annualIncome: '', existingEmiAmount: '0',
  },
  loanDetails: {
    requestedAmount: '', requestedTenureMonths: '60',
    loanPurpose: '', remarks: '',
  },
};

function Field({ label, id, type = 'text', value, onChange, required, placeholder }) {
  return (
    <div>
      <label className="block text-sm font-medium text-gray-700 mb-1" htmlFor={id}>
        {label}{required && <span className="text-red-500 ml-0.5">*</span>}
      </label>
      <input id={id} type={type} value={value} onChange={onChange} placeholder={placeholder}
        className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
    </div>
  );
}

function Select({ label, id, value, onChange, options }) {
  return (
    <div>
      <label className="block text-sm font-medium text-gray-700 mb-1" htmlFor={id}>{label}</label>
      <select id={id} value={value} onChange={onChange}
        className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 bg-white">
        {options.map(o => <option key={o} value={o}>{o}</option>)}
      </select>
    </div>
  );
}

export default function ApplicationWizardPage() {
  const navigate = useNavigate();
  const [step, setStep]       = useState(0);
  const [form, setForm]       = useState(EMPTY);
  const [appId, setAppId]     = useState(null);
  const [saving, setSaving]   = useState(false);
  const [error, setError]     = useState(null);

  const setP = (section, field) => (e) =>
    setForm(f => ({ ...f, [section]: { ...f[section], [field]: e.target.value } }));

  const saveAndNext = async () => {
    setSaving(true); setError(null);
    try {
      const payload = buildPayload(form);
      if (!appId) {
        const { data } = await createApplication(payload);
        setAppId(data.id);
      } else {
        await updateApplication(appId, payload);
      }
      setStep(s => s + 1);
    } catch (err) {
      const errData = err.response?.data;
      setError(errData?.message ?? (errData ? JSON.stringify(errData) : 'Failed to save. Please try again.'));
    } finally {
      setSaving(false);
    }
  };

  const handleSubmit = async () => {
    setSaving(true); setError(null);
    try {
      await submitApplication(appId);
      navigate(`/applications/${appId}/status`);
    } catch (err) {
      const errData = err.response?.data;
      setError(errData?.message ?? (errData ? JSON.stringify(errData) : 'Submission failed.'));
    } finally {
      setSaving(false);
    }
  };

  const p = form.personalDetails;
  const e = form.employmentDetails;
  const l = form.loanDetails;

  // Build a clean payload — convert strings to correct types for the backend
  function buildPayload(f) {
    return {
      personalDetails: {
        ...f.personalDetails,
        dateOfBirth: f.personalDetails.dateOfBirth || null,
      },
      employmentDetails: {
        ...f.employmentDetails,
        monthlyIncome:     f.employmentDetails.monthlyIncome     ? parseFloat(f.employmentDetails.monthlyIncome)     : null,
        annualIncome:      f.employmentDetails.annualIncome      ? parseFloat(f.employmentDetails.annualIncome)      : null,
        existingEmiAmount: f.employmentDetails.existingEmiAmount ? parseFloat(f.employmentDetails.existingEmiAmount) : 0,
      },
      loanDetails: {
        ...f.loanDetails,
        requestedAmount:       f.loanDetails.requestedAmount       ? parseFloat(f.loanDetails.requestedAmount)       : 0,
        requestedTenureMonths: f.loanDetails.requestedTenureMonths ? parseInt(f.loanDetails.requestedTenureMonths)   : 60,
      },
    };
  }

  return (
    <div className="max-w-2xl mx-auto px-4 py-8">
      <h1 className="text-2xl font-bold text-gray-900 mb-2">New Loan Application</h1>

      {/* Step indicator */}
      <div className="flex items-center mb-8">
        {STEPS.map((s, i) => (
          <div key={s} className="flex-1 flex flex-col items-center relative">
            {i > 0 && <div className={`absolute top-3 right-1/2 w-full h-0.5 ${i <= step ? 'bg-[#1e3a5f]' : 'bg-gray-200'}`} />}
            <div className={`relative z-10 w-7 h-7 rounded-full flex items-center justify-center text-xs font-bold
              ${i < step ? 'bg-[#1e3a5f] text-white' : i === step ? 'bg-[#1e3a5f] text-white ring-4 ring-blue-200' : 'bg-gray-200 text-gray-400'}`}>
              {i < step ? '✓' : i + 1}
            </div>
            <span className={`mt-1 text-[10px] font-medium text-center ${i <= step ? 'text-[#1e3a5f]' : 'text-gray-400'}`}>{s}</span>
          </div>
        ))}
      </div>

      {error && <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-lg px-4 py-3 mb-4">{error}</div>}

      <div className="bg-white rounded-2xl border border-gray-200 shadow-sm p-6 space-y-4">

        {/* Step 0 — Personal */}
        {step === 0 && (
          <>
            <div className="grid grid-cols-2 gap-4">
              <Field label="First name" id="fn" value={p.firstName} onChange={setP('personalDetails','firstName')} required />
              <Field label="Last name"  id="ln" value={p.lastName}  onChange={setP('personalDetails','lastName')}  required />
            </div>
            <div className="grid grid-cols-2 gap-4">
              <Field label="Date of birth" id="dob" type="date" value={p.dateOfBirth} onChange={setP('personalDetails','dateOfBirth')} required />
              <Select label="Gender" id="gender" value={p.gender} onChange={setP('personalDetails','gender')} options={['Male','Female','Other']} />
            </div>
            <Field label="Email" id="email" type="email" value={p.email} onChange={setP('personalDetails','email')} required />
            <Field label="Phone" id="phone" type="tel"   value={p.phone} onChange={setP('personalDetails','phone')} required />
            <Field label="Address line 1" id="addr1" value={p.addressLine1} onChange={setP('personalDetails','addressLine1')} required />
            <Field label="Address line 2" id="addr2" value={p.addressLine2} onChange={setP('personalDetails','addressLine2')} />
            <div className="grid grid-cols-3 gap-4">
              <Field label="City"    id="city"  value={p.city}       onChange={setP('personalDetails','city')}       required />
              <Field label="State"   id="state" value={p.state}      onChange={setP('personalDetails','state')}      required />
              <Field label="Pincode" id="pin"   value={p.postalCode} onChange={setP('personalDetails','postalCode')} required />
            </div>
          </>
        )}

        {/* Step 1 — Employment */}
        {step === 1 && (
          <>
            <Field label="Employer name" id="emp" value={e.employerName} onChange={setP('employmentDetails','employerName')} required />
            <Select label="Employment type" id="etype" value={e.employmentType} onChange={setP('employmentDetails','employmentType')}
              options={['Salaried','Self-Employed','Business','Freelancer']} />
            <div className="grid grid-cols-2 gap-4">
              <Field label="Monthly income (₹)" id="mi" type="number" value={e.monthlyIncome} onChange={setP('employmentDetails','monthlyIncome')} required />
              <Field label="Annual income (₹)"  id="ai" type="number" value={e.annualIncome}  onChange={setP('employmentDetails','annualIncome')}  required />
            </div>
            <Field label="Existing EMI obligations (₹)" id="emi" type="number" value={e.existingEmiAmount} onChange={setP('employmentDetails','existingEmiAmount')} />
          </>
        )}

        {/* Step 2 — Loan details */}
        {step === 2 && (
          <>
            <Field label="Loan amount (₹)" id="amt" type="number" value={l.requestedAmount} onChange={setP('loanDetails','requestedAmount')} required placeholder="10000 – 5000000" />
            <Field label="Tenure (months)" id="ten" type="number" value={l.requestedTenureMonths} onChange={setP('loanDetails','requestedTenureMonths')} required placeholder="6 – 360" />
            <Field label="Loan purpose" id="purpose" value={l.loanPurpose} onChange={setP('loanDetails','loanPurpose')} required placeholder="Home renovation, education…" />
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Remarks</label>
              <textarea value={l.remarks} onChange={setP('loanDetails','remarks')} rows={3}
                className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
            </div>
          </>
        )}

        {/* Step 3 — Review */}
        {step === 3 && (
          <div className="space-y-4 text-sm">
            <Section title="Personal Details">
              <Row label="Name"    value={`${p.firstName} ${p.lastName}`} />
              <Row label="Email"   value={p.email} />
              <Row label="Phone"   value={p.phone} />
              <Row label="Address" value={`${p.addressLine1}, ${p.city}, ${p.state} ${p.postalCode}`} />
            </Section>
            <Section title="Employment">
              <Row label="Employer"       value={e.employerName} />
              <Row label="Type"           value={e.employmentType} />
              <Row label="Monthly income" value={`₹${Number(e.monthlyIncome).toLocaleString()}`} />
              <Row label="Existing EMI"   value={`₹${Number(e.existingEmiAmount).toLocaleString()}`} />
            </Section>
            <Section title="Loan Details">
              <Row label="Amount"  value={`₹${Number(l.requestedAmount).toLocaleString()}`} />
              <Row label="Tenure"  value={`${l.requestedTenureMonths} months`} />
              <Row label="Purpose" value={l.loanPurpose} />
            </Section>
          </div>
        )}

        {/* Navigation */}
        <div className="flex justify-between pt-2">
          <button onClick={() => setStep(s => s - 1)} disabled={step === 0 || saving}
            className="px-4 py-2 border border-gray-300 text-gray-700 text-sm rounded-lg hover:bg-gray-50 disabled:opacity-40">
            ← Back
          </button>
          {step < 3
            ? <button onClick={saveAndNext} disabled={saving}
                className="px-5 py-2 bg-[#1e3a5f] hover:bg-[#0f2744] text-white text-sm font-semibold rounded-lg disabled:opacity-60">
                {saving ? 'Saving…' : 'Save & Continue →'}
              </button>
            : <button onClick={handleSubmit} disabled={saving}
                className="px-5 py-2 bg-green-600 hover:bg-green-700 text-white text-sm font-semibold rounded-lg disabled:opacity-60">
                {saving ? 'Submitting…' : '✓ Submit Application'}
              </button>
          }
        </div>
      </div>
    </div>
  );
}

function Section({ title, children }) {
  return (
    <div>
      <h3 className="font-semibold text-gray-800 mb-2 border-b pb-1">{title}</h3>
      <dl className="grid grid-cols-[max-content_1fr] gap-x-6 gap-y-1">{children}</dl>
    </div>
  );
}

function Row({ label, value }) {
  return (
    <>
      <dt className="text-gray-500 text-xs py-0.5">{label}</dt>
      <dd className="text-gray-800 text-xs py-0.5">{value || '—'}</dd>
    </>
  );
}
