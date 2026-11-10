import { ApiError } from "./api";

const MESSAGES: Record<string, string> = {
  NO_SEATS: "এই class-এর সব seat এখন নেওয়া হয়ে গেছে।",
  ALREADY_ACTIVE: "এই class-এ আপনার একটি আবেদন বা ভর্তি আগে থেকেই আছে।",
  HOLD_EXPIRED: "সময় শেষ হয়ে গেছে, তাই seat ফেরত গেছে। আবার আবেদন করুন।",
  HOLD_NOT_ACTIVE: "এই আবেদনটি আর সক্রিয় নেই।",
  HOLD_NOT_FOUND: "আবেদনটি পাওয়া যায়নি।",
  CLASS_NOT_FOUND: "Class-টি পাওয়া যায়নি।",
  CANNOT_CANCEL_CONFIRMED: "ভর্তি নিশ্চিত হওয়া আবেদন বাতিল করা যাবে না।",
  INVALID_STUDENT_ID: "জন্ম নিবন্ধন নম্বর ১৭ ডিজিটের হতে হবে।",
  VALIDATION_ERROR: "তথ্যে ভুল আছে, আবার দেখুন।",
  NETWORK: "সার্ভারের সাথে যোগাযোগ করা যাচ্ছে না। একটু পরে আবার চেষ্টা করুন।",
};

export function errorMessage(e: unknown): string {
  if (e instanceof ApiError && MESSAGES[e.code]) return MESSAGES[e.code];
  return "কিছু একটা ভুল হয়েছে। আবার চেষ্টা করুন।";
}
