import { Shield } from "lucide-react";

export default function GearPage() {
  return (
    <div className="p-4">
      <div className="flex items-center gap-2 mb-4">
        <Shield className="text-gold" size={24} />
        <h1 className="text-xl font-bold">Gear Sets</h1>
      </div>
      <p className="text-text-secondary">
        Manage your crafting and gathering gear sets. Coming soon.
      </p>
    </div>
  );
}
