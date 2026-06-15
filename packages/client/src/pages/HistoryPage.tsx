import { useCallback, useState } from "react";
import { ArrowLeft } from "lucide-react";
import { Header } from "@/components/layout/Header";
import { Footer } from "@/components/layout/Footer";
import { AuthDialog } from "@/components/auth/AuthDialog";
import { AnalysisHistoryPanel } from "@/components/home/AnalysisHistoryPanel";
import { Button } from "@/components/ui/button";
import { useAuth } from "@/hooks/useAuth";

export function HistoryPage() {
  const auth = useAuth();
  const [authDialogOpen, setAuthDialogOpen] = useState(false);

  const handleRequireAuth = useCallback(() => {
    setAuthDialogOpen(true);
  }, []);

  return (
    <div className="grain min-h-screen bg-background text-foreground">
      <Header
        user={auth.user}
        onAuthClick={() => setAuthDialogOpen(true)}
        onLogout={auth.logout}
      />

      <main className="pb-10 pt-24 sm:pt-28">
        <section className="px-4 py-6 sm:px-6 lg:px-8">
          <div className="mx-auto max-w-6xl">
            {/* 顶部导航 */}
            <div className="mb-6 flex items-center gap-4">
              <Button
                variant="outline"
                size="sm"
                onClick={() => (window.location.href = "/")}
                className="gap-1.5"
              >
                <ArrowLeft className="h-4 w-4" />
                返回首页
              </Button>
            </div>

            <AnalysisHistoryPanel
              token={auth.token}
              onRequireAuth={handleRequireAuth}
            />
          </div>
        </section>
      </main>

      <Footer />

      <AuthDialog
        open={authDialogOpen}
        onOpenChange={setAuthDialogOpen}
        onLogin={auth.login}
        onRegister={auth.register}
      />
    </div>
  );
}
