import { FlaskConical } from "lucide-react";

export default function SimulatorPage() {
  return (
    <div className="p-4">
      <div className="flex items-center gap-2 mb-4">
        <FlaskConical className="text-gold" size={24} />
        <h1 className="text-xl font-bold">Crafting Simulator</h1>
      </div>
      <p className="text-text-secondary">
        Simulate crafting rotations to optimize your crafts. Coming soon.
      </p>
    </div>
  );
}
