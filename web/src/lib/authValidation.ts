export type EmailValidationReason =
  | "empty"
  | "invalid"
  | "checking"
  | "taken"
  | "network"
  | "available";

export type PasswordValidationReason =
  | "empty"
  | "length"
  | "uppercase"
  | "lowercase"
  | "digit"
  | "common"
  | "email";

export type FieldValidationState<TReason extends string> = {
  touched: boolean;
  valid: boolean;
  reason: TReason | null;
};

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const commonPasswords = new Set([
  "12345678",
  "password",
  "password123",
  "qwerty123",
  "admin123",
  "11111111",
]);

export function createIdleValidation<TReason extends string>(): FieldValidationState<TReason> {
  return { touched: false, valid: true, reason: null };
}

export function validateEmailFormat(value: string): FieldValidationState<EmailValidationReason> {
  const email = value.trim().toLowerCase();
  if (!email) return { touched: true, valid: false, reason: "empty" };
  if (!emailPattern.test(email)) {
    return { touched: true, valid: false, reason: "invalid" };
  }
  return { touched: true, valid: true, reason: null };
}

export function validatePassword(
  value: string,
  email: string,
): FieldValidationState<PasswordValidationReason> {
  const password = value.trim();
  if (!password) return { touched: true, valid: false, reason: "empty" };
  if (password.length < 8) return { touched: true, valid: false, reason: "length" };
  if (!/[A-Z]/.test(password)) return { touched: true, valid: false, reason: "uppercase" };
  if (!/[a-z]/.test(password)) return { touched: true, valid: false, reason: "lowercase" };
  if (!/\d/.test(password)) return { touched: true, valid: false, reason: "digit" };
  if (commonPasswords.has(password.toLowerCase())) {
    return { touched: true, valid: false, reason: "common" };
  }

  const emailHead = email.trim().split("@")[0]?.toLowerCase();
  if (emailHead && password.toLowerCase().includes(emailHead)) {
    return { touched: true, valid: false, reason: "email" };
  }

  return { touched: true, valid: true, reason: null };
}

export function validatePasswordConfirmation(
  password: string,
  confirmPassword: string,
): FieldValidationState<"empty" | "mismatch"> {
  if (!confirmPassword.trim()) {
    return { touched: true, valid: false, reason: "empty" };
  }
  if (password !== confirmPassword) {
    return { touched: true, valid: false, reason: "mismatch" };
  }
  return { touched: true, valid: true, reason: null };
}
