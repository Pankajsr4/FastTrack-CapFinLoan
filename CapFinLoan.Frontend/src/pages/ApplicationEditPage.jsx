import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { getApplicationById, updateApplication } from '../services/applicationService';

const EDITABLE_STATUSES = new Set(['Draft', 'Submitted', 'Docs Pending', 'Docs Verified']);

function Field({ label, id, type = 'text', value, onChange, required, placeholder, disabled }) {
  return (
    <div>
      <label className="block text-sm font-medium text-gray-700 mb-1" htmlFor={id}>
        {label}{required && <span className="text-red-500 ml-0.5">*</span>}
      </label>
      <input id={id} type={type} value={value} onChange={onChange} placeholder={placeholder}
        disabled={disabled}
        className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:bg-gray-50 disabled:text-gray-400" />
    </div>
  );
}

function Select({ label, id, value, onChange, options, disabled }) {
  return (
    <div>
      <label className="block text-sm font-medium text-gray-700 mb-1" htmlFor={id}>{label}</label>
      <select id={id} value={value} onChange={onChange} disabled={disabled}
        className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 bg-white disabled:bg-gray-50">
        {options.map(o => <option key={o} value={o}>{o}</option>)}
      </select>
    </div>
  );
}

export default function ApplicationEditPage() {
  const { id } = useParams();
  const navigate = useNavigate();

  const [form,    setForm]    = useState(null);
  const [loading, setLoading] = useState(true);
  const [saving,  setSaving]  = useState(false);
  const [error,   setError]   = useState(null);
  const [appStatus, setAppStatus] = useState('');

  useEffect(() => {
    getApplicationById(id)
      .then(({ data }) => {
        setAppStatus(data.status);
        setForm({
          personalDetails: {
            firstName:   data.personalDetails?.firstName   ?? data.firstName   ?? '',
            lastName:    data.personalDetails?.lastName    ?? data.lastName    ?? '',
            dateOfBirth: data.personalDetails?.dateOfBirth ?? data.dateOfBirth
              ? (data.personalDetails?.dateOfBirth ?? data.dateOfBirth).split('T')[0]
              : '',
            gender:      data.personalDetails?.gender      ?? data.gender      ?? '',
            email:       data.personalDetails?.email       ?? data.email       ?? '',
            phone:       data.personalDetails?.phone       ?? data.phone       ?? '',
            addressLine1:data.personalDetails?.addressLine1?? data.addressLine1?? '',
            addressLine2:data.personalDetails?.addressLine2?? data.addressLine2?? '',
            city:        data.personalDetails?.city        ?? data.city        ?? '',
            state:       data.personalDetails?.state       ?? data.state       ?? '',
            postalCode:  data.personalDetails?.postalCode  ?? data.postalCode  ?? '',
          },
          employmentDetails: {
            employerName:     data.employmentDetails?.employerName     ?? data.employerName     ?? '',
            employmentType:   data.employmentDetails?.employmentType   ?? data.employmentType   ?? 'Salaried',
            monthlyIncome:    String(data.employmentDetails?.monthlyIncome  ?? data.monthlyIncome  ?? ''),
            annualIncome:     String(data.employmentDetails?.annualIncome   ?? data.annualIncome   ?? ''),
            existingEmiAmount:String(data.employmentDetails?.existingEmiAmount ?? data.existingEmiAmount ?? '0'),
          },
          loanDetails: {
            requestedAmount:       String(data.loanDetails?.requestedAmount       ?? data.requestedAmount       ?? ''),
            requestedTenureMonths: String(data.loanDetails?.requestedTenureMonths ?? data.requestedTenureMonths ?? '60'),
            loanPurpose:           data.loanDetails?.loanPurpose ?? data.loanPurpose ?? '',
            remarks:               data.loanDetails?.remarks     ?? data.remarks     ?? '',
          },
        });
      })
      .catch(e => setError(e.response?.data?.message ?? 'Failed to load application.'))
      .finally(() => setLoading(false));
  }, [id]);

  const canEdit = EDITABLE_STATUSES.has(appStatus);

  const setP = (section, field) => (e) =>
    setForm(f => ({ ...f, [section]: { ...f[section], [field]: e.target.value } }));

  const handleSave = async () => {
    setSaving(true); setError(null);
    try {
      const payload = {
        personalDetails: {
          ...form.personalDetails,
          dateOfBirth: form.personalDetails.dateOfBirth || null,
          addressLine2: form.personalDetails.addressLine2 || '',
        },
        employmentDetails: {
          ...form.employmentDetails,
          monthlyIncome:     form.employmentDetails.monthlyIncome     ? parseFloat(form.employmentDetails.monthlyIncome)     : null,
          annualIncome:      form.employmentDetails.annualIncome      ? parseFloat(form.employmentDetails.annualIncome)      : null,
          existingEmiAmount: form.employmentDetails.existingEmiAmount ? parseFloat(form.employmentDetails.existingEmiAmount) : 0,
        },
        loanDetails: {
          ...form.loanDetails,
          remarks:               form.loanDetails.remarks || '',
          requestedAmount:       form.loanDetails.requestedAmount       ? parseFloat(form.loanDetails.requestedAmount)       : 0,
          requestedTenureMonths: form.loanDetails.requestedTenureMonths ? parseInt(form.loanDetails.requestedTenureMonths)   : 60,
        },
      };
      await updateApplication(id, payload);
      navigate(`/applications/${id}/status`, { state: { success: 'Application updated successfully.' } });
    } catch (e) {
      const msg = e.response?.data?.message ?? e.response?.data?.errors
        ? JSON.stringify(e.response.data.errors)
        : e.message ?? 'Failed to save changes.';
      setError(typeof msg === 'string' ? msg : JSON.stringify(msg));
      // Scroll to top so user sees the error
      window.scrollTo({ top: 0, behavior: 'smooth' });
    } finally {
      setSaving(false);
    }
  };

  if (loading) return (
    <div className="flex justify-center py-16">
      <div className="w-8 h-8 border-4 border-gray-200 border-t-[#1e3a5f] rounded-full animate-spin" />
    </div>
  );

  if (!canEdit) return (
    <div className="max-w-2xl mx-auto px-4 py-8 text-center">
      <p className="text-red-600 font-medium">
        This application cannot be edited in its current status ({appStatus}).
      </p>
      <button onClick={() => navigate(-1)} className="mt-4 text-sm text-blue-600 hover:underline">← Go back</button>
    </div>
  );

  const p = form.personalDetails;
  const e = form.employmentDetails;
  const l = form.loanDetails;

  return (
    <div className="max-w-2xl mx-auto px-4 py-8 space-y-6">
      <div className="flex items-center gap-3">
        <button onClick={() => navigate(-1)} className="text-gray-500 hover:text-gray-800 text-sm">← Back</button>
        <h1 className="text-2xl font-bold text-gray-900">Edit Application</h1>
        <span className="text-xs bg-blue-100 text-blue-700 px-2 py-0.5 rounded-full font-semibold">{appStatus}</span>
      </div>

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-lg px-4 py-3">{error}</div>
      )}

      <div className="bg-white rounded-2xl border border-gray-200 shadow-sm p-6 space-y-5">
        <h2 className="font-semibold text-gray-800 border-b pb-2">Personal Details</h2>
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
      </div>

      <div className="bg-white rounded-2xl border border-gray-200 shadow-sm p-6 space-y-5">
        <h2 className="font-semibold text-gray-800 border-b pb-2">Employment Details</h2>
        <Field label="Employer name" id="emp" value={e.employerName} onChange={setP('employmentDetails','employerName')} required />
        <Select label="Employment type" id="etype" value={e.employmentType} onChange={setP('employmentDetails','employmentType')}
          options={['Salaried','Self-Employed','Business','Freelancer']} />
        <div className="grid grid-cols-2 gap-4">
          <Field label="Monthly income (₹)" id="mi" type="number" value={e.monthlyIncome} onChange={setP('employmentDetails','monthlyIncome')} required />
          <Field label="Annual income (₹)"  id="ai" type="number" value={e.annualIncome}  onChange={setP('employmentDetails','annualIncome')}  required />
        </div>
        <Field label="Existing EMI (₹)" id="emi" type="number" value={e.existingEmiAmount} onChange={setP('employmentDetails','existingEmiAmount')} />
      </div>

      <div className="bg-white rounded-2xl border border-gray-200 shadow-sm p-6 space-y-5">
        <h2 className="font-semibold text-gray-800 border-b pb-2">Loan Details</h2>
        <Field label="Loan amount (₹)" id="amt" type="number" value={l.requestedAmount} onChange={setP('loanDetails','requestedAmount')} required />
        <Field label="Tenure (months)" id="ten" type="number" value={l.requestedTenureMonths} onChange={setP('loanDetails','requestedTenureMonths')} required />
        <Field label="Loan purpose" id="purpose" value={l.loanPurpose} onChange={setP('loanDetails','loanPurpose')} required />
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Remarks</label>
          <textarea value={l.remarks} onChange={setP('loanDetails','remarks')} rows={3}
            className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
        </div>
      </div>

      <div className="flex justify-end gap-3">
        <button onClick={() => navigate(-1)} disabled={saving}
          className="px-4 py-2 border border-gray-300 text-gray-700 text-sm rounded-lg hover:bg-gray-50 disabled:opacity-50">
          Cancel
        </button>
        <button onClick={handleSave} disabled={saving}
          className="px-5 py-2 bg-[#1e3a5f] hover:bg-[#0f2744] text-white text-sm font-semibold rounded-lg disabled:opacity-60">
          {saving ? 'Saving…' : '💾 Save Changes'}
        </button>
      </div>
    </div>
  );
}
