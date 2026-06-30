import { useAppState } from "../context/AppContext";
import { sectionHeading } from "../utils/classes";

const VERSION = "0.1.1";
const REPO_URL = "https://github.com/clevr/clevr-lint-extension";
const ISSUES_URL = "https://github.com/ClevrSolutions/mxcli-linter-studio-extension/issues";

const CHANGELOG: { version: string; changes: string[] }[] = [
  {
    version: "0.1.1",
    changes: [
      "Add per-module filter section to FilterBar",
      "Add About tab and auto-fetch files when adding a new rule source",
      "Fix rule source data loss, partial delete abort, path traversal, and code duplication",
    ],
  },
  {
    version: "0.1.0",
    changes: [
      "Add lint rule sources: fetch, replace, and delete rule files from GitHub directories",
      "Add baseline comparison feature for tracking new and fixed violations",
      "Add mxcli lifecycle management: auto-detect, download, and custom location picker",
      "Add git change tracking and scoped scan support",
      "Add linter config store with per-rule enable/severity overrides",
      "Add settings UI with Modules, Rules, Configuration, and Sources tabs",
      "Add HTML report export",
      "Add exclusion management with fingerprint matching",
      "Initial release: mxcli-based linting inside Mendix Studio Pro 11",
    ],
  },
];

const CONTRIBUTORS = [
  { name: "Andries Smit", role: "Author" },
];

const link = "text-clevr-accent hover:underline";
const badge = "inline-block px-2 py-px rounded-full text-[11px] font-semibold bg-[#e7eef6] text-clevr-accent";

export function AboutTab() {
  const state = useAppState();
  const mxcliVersion = state.mxcliInfo?.version ?? null;

  return (
    <div className="max-w-[600px]">
      <section className="mb-6 pb-4 border-b border-clevr-border">
        <div className="text-[18px] font-bold mb-1">CLEVR Lint</div>
        <div className="text-[13px] text-clevr-muted mb-2">
          Mendix Studio Pro 11 extension — mxcli linting rules inside your IDE
        </div>
        <div className="flex gap-2 mt-1">
          <span className={badge}>v{VERSION}</span>
          {mxcliVersion && (
            <span className="inline-block px-2 py-px rounded-full text-[11px] font-semibold bg-clevr-card text-clevr-muted">
              mxcli {mxcliVersion}
            </span>
          )}
        </div>
      </section>

      <section className="mb-6">
        <h3 className={sectionHeading}>Copyright &amp; License</h3>
        <p className="text-[13px] mb-2">
          © 2026{" "}
          <a className={link} href={REPO_URL} target="_blank" rel="noreferrer">CLEVR</a>
          . Released under the{" "}
          <a className={link} href={`${REPO_URL}/blob/main/LICENSE`} target="_blank" rel="noreferrer">MIT License</a>.
        </p>
        <p className="text-[13px] mb-2">
          Built with{" "}
          <a className={link} href="https://clevr.com" target="_blank" rel="noreferrer">clevr.com</a>{" "}
          tooling. Powered by{" "}
          <a className={link} href="https://github.com/mendix/mxcli" target="_blank" rel="noreferrer">mxcli</a>{" "}
          (Apache-2.0).
        </p>
      </section>

      <section className="mb-6">
        <h3 className={sectionHeading}>Source</h3>
        <p className="text-[13px] mb-2">
          <a className={link} href={REPO_URL} target="_blank" rel="noreferrer">{REPO_URL}</a>
        </p>
      </section>

      <section className="mb-6">
        <h3 className={sectionHeading}>Feedback &amp; Support</h3>
        <p className="text-[13px] mb-2">
          Found a bug, have a feature request, or want to suggest a new lint rule?{" "}
          <a className={link} href={ISSUES_URL} target="_blank" rel="noreferrer">Open an issue on GitHub</a>.
        </p>
        <p className="text-[13px] mb-2">
          When reporting an issue, please include your mxcli version, a description of the problem, and steps to reproduce it.
        </p>
      </section>

      <section className="mb-6">
        <h3 className={sectionHeading}>Contributors</h3>
        <table className="w-full text-[12px]">
          <tbody>
            {CONTRIBUTORS.map((c) => (
              <tr key={c.name}>
                <td className="font-medium py-1 pr-3">{c.name}</td>
                <td className="text-clevr-muted py-1">{c.role}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      <section className="mb-6">
        <h3 className={sectionHeading}>Changelog</h3>
        {CHANGELOG.map(({ version, changes }) => (
          <div key={version} className="mb-4">
            <div className="mb-1.5">
              <span className={badge}>v{version}</span>
            </div>
            <ul className="text-[12px] pl-4 m-0 space-y-0.5 list-disc">
              {changes.map((c) => (
                <li key={c}>{c}</li>
              ))}
            </ul>
          </div>
        ))}
      </section>
    </div>
  );
}
