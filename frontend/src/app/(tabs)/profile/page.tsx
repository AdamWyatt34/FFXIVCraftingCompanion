import { User } from "lucide-react";

export default function ProfilePage() {
  return (
    <div className="p-4">
      <div className="flex items-center gap-2 mb-4">
        <User className="text-gold" size={24} />
        <h1 className="text-xl font-bold">Profile</h1>
      </div>
      <p className="text-text-secondary">
        Set your crafter stats and preferences. Coming soon.
      </p>
    </div>
  );
}
