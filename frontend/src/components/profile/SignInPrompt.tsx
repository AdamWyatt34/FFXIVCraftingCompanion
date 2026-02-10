import Link from "next/link";
import { User, Bookmark, Shield, BarChart3 } from "lucide-react";

const benefits = [
  { icon: Bookmark, label: "Save shopping lists and crafting plans" },
  { icon: Shield, label: "Personalized gear recommendations" },
  { icon: BarChart3, label: "Track your crafter & gatherer progress" },
];

export function SignInPrompt() {
  return (
    <div className="flex-1 flex items-center justify-center p-6">
      <div className="text-center space-y-6 max-w-sm">
        <div className="mx-auto w-16 h-16 rounded-full bg-gold/10 flex items-center justify-center">
          <User className="text-gold" size={32} />
        </div>

        <div>
          <h2 className="text-xl font-bold text-text-primary mb-2">
            Sign in to access your profile
          </h2>
          <p className="text-text-secondary text-sm">
            Create a profile to unlock personalized features
          </p>
        </div>

        <div className="space-y-3 text-left">
          {benefits.map((b) => (
            <div key={b.label} className="flex items-center gap-3 text-sm">
              <b.icon className="text-gold shrink-0" size={18} />
              <span className="text-text-secondary">{b.label}</span>
            </div>
          ))}
        </div>

        <div className="space-y-3">
          <Link
            href="/auth/login"
            className="block w-full h-11 bg-gold text-bg-primary font-semibold rounded-lg hover:bg-gold-light transition-colors leading-[2.75rem] text-center"
          >
            Sign In
          </Link>
          <Link
            href="/auth/register"
            className="block w-full h-11 border border-border text-text-primary font-medium rounded-lg hover:border-border-focus transition-colors leading-[2.75rem] text-center"
          >
            Create Account
          </Link>
        </div>
      </div>
    </div>
  );
}
