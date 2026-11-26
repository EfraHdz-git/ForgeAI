export function passwordChecks(pw) {
  return {
    length: pw.length >= 8,
    uppercase: /[A-Z]/.test(pw),
    number: /[0-9]/.test(pw),
    special: /[^a-zA-Z0-9]/.test(pw),
  };
}

export function isPasswordValid(pw) {
  const c = passwordChecks(pw);
  return c.length && c.uppercase && c.number && c.special;
}
