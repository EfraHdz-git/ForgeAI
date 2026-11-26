import { useState } from 'react';
import { postJson } from '../hooks/useApi';

const SPECIFICITY_WORDS = /\b(when|because|but|don't|can't|won't|waste|forget|lose|miss|fail|struggle|stuck|frustrated|no way|end up|have to|instead of|every time|during|after|before)\b/i;
const CONSEQUENCE_WORDS = /\b(so they|which means|leading to|resulting in|causing|end up|miss out|lose|waste|pay more|give up|quit|abandon)\b/i;

function assessQuality(text) {
  const len = text.trim().length;
  if (len < 20) return { level: 'empty', label: '', hint: '' };
  if (len < 40) return { level: 'vague', label: 'Too vague', hint: 'What exactly goes wrong? What\'s the consequence?' };

  const hasSpecificity = SPECIFICITY_WORDS.test(text);
  const hasConsequence = CONSEQUENCE_WORDS.test(text);
  const hasBothClauses = text.includes(',') || text.includes('—') || text.includes('and') || text.includes('but');

  if (len >= 80 && hasSpecificity && (hasConsequence || hasBothClauses))
    return { level: 'strong', label: 'Strong problem', hint: '' };

  if (len >= 40 && (hasSpecificity || hasBothClauses))
    return { level: 'decent', label: 'Decent', hint: 'Add what happens as a result — what do they lose or miss?' };

  return { level: 'decent', label: 'Decent', hint: 'When does this happen? What\'s the consequence?' };
}

export default function SubmitIdea({ onBack, onToast, onProcessing }) {
  const [form, setForm] = useState({ problem: '', who: '', when: '', solution: '' });
  const [submitting, setSubmitting] = useState(false);
  const [touched, setTouched] = useState({});

  const set = (field, value) => setForm(f => ({ ...f, [field]: value }));
  const blur = (field) => setTouched(t => ({ ...t, [field]: true }));

  const quality = assessQuality(form.problem);
  const problemOk = form.problem.trim().length >= 40;
  const whoOk = form.who.trim().length >= 5;

  const submit = async () => {
    setTouched({ problem: true, who: true });
    if (!problemOk || !whoOk) return;
    setSubmitting(true);
    try {
      const res = await postJson('/dashboard/opportunities/submit', {
        problem: form.problem.trim(),
        who: form.who.trim(),
        when: form.when.trim() || undefined,
        useCase: form.solution.trim() || undefined,
      });
      onToast?.('Idea submitted — generating your report', 'success');
      if (onProcessing) onProcessing(res.id);
    } catch (err) {
      console.error(err);
      onToast?.('Failed to submit. Please try again.', 'error');
    }
    setSubmitting(false);
  };

  return (
    <div className="submit-page">
      <div className="submit-header">
        <h2>Get a real market analysis for your idea</h2>
        <p className="submit-subtitle">Describe a real problem. We'll research competitors, analyze gaps, and deliver an honest report in under 10 minutes.</p>
      </div>

      <div className="submit-form">
        <div className="submit-card">
          <div className={`form-group ${touched.problem && !problemOk ? 'form-error' : ''}`}>
            <label>What problem are you trying to solve?</label>
            <textarea
              value={form.problem}
              onChange={e => set('problem', e.target.value)}
              onBlur={() => blur('problem')}
              placeholder="Gym beginners forget their routine mid-workout and waste time scrolling through notes or watching YouTube between sets."
              rows={4}
            />
            <div className="form-field-footer">
              <div className="form-field-feedback">
                {touched.problem && !problemOk && <span className="field-error">This sounds broad. What exactly goes wrong?</span>}
                {problemOk && quality.hint && <span className="field-hint">{quality.hint}</span>}
              </div>
              <div className="quality-indicator-row">
                {quality.level !== 'empty' && (
                  <span className={`quality-badge quality-${quality.level}`}>{quality.label}</span>
                )}
                <span className="char-count">{form.problem.length}</span>
              </div>
            </div>
          </div>

          <div className={`form-group ${touched.who && !whoOk ? 'form-error' : ''}`}>
            <label>Who has this problem?</label>
            <input
              type="text"
              value={form.who}
              onChange={e => set('who', e.target.value)}
              onBlur={() => blur('who')}
              placeholder="People new to lifting in their first 3 months"
            />
            {touched.who && !whoOk && <span className="field-error">Be specific — who exactly?</span>}
          </div>

          <div className="form-group">
            <label>When does this problem usually happen? <span className="label-optional">optional but helps</span></label>
            <input
              type="text"
              value={form.when}
              onChange={e => set('when', e.target.value)}
              placeholder="At the gym, between sets"
            />
          </div>

          <div className="form-group">
            <label>How do you imagine the solution? <span className="label-optional">optional but highly recommended</span></label>
            <textarea
              value={form.solution}
              onChange={e => set('solution', e.target.value)}
              placeholder="Describe what the tool would actually do. e.g. An app that shows the next exercise with reps and rest timer, auto-adjusts based on how the last set felt."
              rows={3}
            />
            <p className="form-sub-hint">Think about the expected output, the workflow, what makes it different. This helps us position your idea accurately.</p>
          </div>

          <p className="form-hint">Be specific. Real situations produce better results than broad ideas.</p>

          <div className="form-footer">
            <button className="btn-brand btn-submit-main" onClick={submit} disabled={submitting || !problemOk || !whoOk}>
              {submitting ? 'Generating...' : 'Generate My Report'}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
