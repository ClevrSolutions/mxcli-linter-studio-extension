import { useAppState } from "../context/AppContext";

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

export function AboutTab() {
  const state = useAppState();
  const mxcliVersion = state.mxcliInfo?.version ?? null;

  return (
    <div className="lint-about">
      <section className="lint-about-section lint-about-identity">
        <div className="lint-about-name">CLEVR Lint</div>
        <div className="lint-about-tagline">
          Mendix Studio Pro 11 extension — mxcli linting rules inside your IDE
        </div>
        <div className="lint-about-badges">
          <span className="lint-about-badge">v{VERSION}</span>
          {mxcliVersion && (
            <span className="lint-about-badge lint-about-badge-muted">mxcli {mxcliVersion}</span>
          )}
        </div>
      </section>

      <section className="lint-about-section">
        <h3 className="lint-about-heading">Copyright &amp; License</h3>
        <p className="lint-about-para">
          © 2026{" "}
          <a className="lint-about-link" href={REPO_URL} target="_blank" rel="noreferrer">
            CLEVR
          </a>
          . Released under the{" "}
          <a className="lint-about-link" href={`${REPO_URL}/blob/main/LICENSE`} target="_blank" rel="noreferrer">
            MIT License
          </a>
          .
        </p>
        <p className="lint-about-para">
          Built with{" "}
          <a className="lint-about-link" href="https://clevr.com" target="_blank" rel="noreferrer">
            clevr.com
          </a>{" "}
          tooling. Powered by{" "}
          <a className="lint-about-link" href="https://github.com/mendix/mxcli" target="_blank" rel="noreferrer">
            mxcli
          </a>{" "}
          (Apache-2.0).
        </p>
      </section>

      <section className="lint-about-section">
        <h3 className="lint-about-heading">Source</h3>
        <p className="lint-about-para">
          <a className="lint-about-link" href={REPO_URL} target="_blank" rel="noreferrer">
            {REPO_URL}
          </a>
        </p>
      </section>

      <section className="lint-about-section">
        <h3 className="lint-about-heading">Feedback &amp; Support</h3>
        <p className="lint-about-para">
          Found a bug, have a feature request, or want to suggest a new lint rule?{" "}
          <a className="lint-about-link" href={ISSUES_URL} target="_blank" rel="noreferrer">
            Open an issue on GitHub
          </a>
          .
        </p>
        <p className="lint-about-para">
          When reporting an issue, please include your mxcli version, a description of the problem, and steps to reproduce it.
        </p>
      </section>

      <section className="lint-about-section">
        <h3 className="lint-about-heading">Contributors</h3>
        <table className="lint-settings-table lint-about-table">
          <tbody>
            {CONTRIBUTORS.map((c) => (
              <tr key={c.name}>
                <td className="lint-about-contributor-name">{c.name}</td>
                <td className="lint-about-contributor-role">{c.role}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      <section className="lint-about-section">
        <h3 className="lint-about-heading">Changelog</h3>
        {CHANGELOG.map(({ version, changes }) => (
          <div key={version} className="lint-about-release">
            <div className="lint-about-release-header">
              <span className="lint-about-badge">v{version}</span>
            </div>
            <ul className="lint-about-changes">
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
