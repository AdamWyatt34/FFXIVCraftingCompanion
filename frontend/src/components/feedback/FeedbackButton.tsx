"use client";

import { useState } from "react";
import { MessageSquarePlus } from "lucide-react";
import { FeedbackModal } from "./FeedbackModal";

export function FeedbackButton() {
  const [open, setOpen] = useState(false);

  return (
    <>
      <button
        onClick={() => setOpen(true)}
        aria-label="Send feedback"
        className="fixed bottom-20 right-4 z-40 w-12 h-12 rounded-full bg-gold text-bg-primary shadow-lg hover:bg-gold-light transition-colors flex items-center justify-center"
      >
        <MessageSquarePlus size={22} />
      </button>
      <FeedbackModal open={open} onClose={() => setOpen(false)} />
    </>
  );
}
