"use client";

import { useState } from "react";
import { ConfirmDialog } from "@/components/ui/ConfirmDialog";
import { useToast } from "@/components/ui/Toast";
import { updateCurrentGear } from "@/lib/api-client";

interface ApplyGearDialogProps {
  open: boolean;
  onClose: () => void;
  slots: Record<string, number>;
  mode: "all" | "single";
  slotName?: string;
  onSuccess: () => void;
}

export function ApplyGearDialog({
  open,
  onClose,
  slots,
  mode,
  slotName,
  onSuccess,
}: ApplyGearDialogProps) {
  const [isLoading, setIsLoading] = useState(false);
  const { toast } = useToast();

  const slotCount = Object.keys(slots).length;

  const title =
    mode === "all"
      ? "Apply recommended gear to profile?"
      : `Use ${slotName ?? "this"} recommendation?`;

  const message =
    mode === "all"
      ? `This will update ${slotCount} gear slot${slotCount !== 1 ? "s" : ""} in your profile to the recommended equipment.`
      : `This will update your ${slotName ?? "gear"} slot to the recommended equipment.`;

  const handleConfirm = async () => {
    setIsLoading(true);
    try {
      await updateCurrentGear(slots);
      toast(
        mode === "all"
          ? "All recommended gear applied to profile!"
          : `${slotName ?? "Gear"} updated in profile!`,
        "success",
      );
      onSuccess();
      onClose();
    } catch {
      toast("Failed to update profile gear", "error");
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <ConfirmDialog
      open={open}
      title={title}
      message={message}
      confirmLabel={mode === "all" ? "Apply all" : "Apply"}
      isLoading={isLoading}
      onConfirm={handleConfirm}
      onCancel={onClose}
    />
  );
}
