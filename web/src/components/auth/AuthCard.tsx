import type { ReactNode } from "react";

import { TopNav } from "../top/TopNav";

export function AuthCard({
  title,
  subtitle,
  children,
}: {
  title: string;
  subtitle?: string;
  children: ReactNode;
}) {
  return (
    <div className="wrap auth-page-wrap">
      <TopNav variant="auth" />
      <div className="auth-wrap">
        <div className="auth-shell auth-shell-single">
          <div className="card auth-card">
            <div className="auth-card-copy">
              <h2>{title}</h2>
              {subtitle ? <p>{subtitle}</p> : null}
            </div>
            {children}
          </div>
        </div>
      </div>
    </div>
  );
}
