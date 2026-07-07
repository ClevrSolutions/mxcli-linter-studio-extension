import { useAppDispatch, useAppState } from "../context/AppContext";
import { bulkBtn, sectionHeading, settingsDesc, settingsEmpty, tableBase } from "../utils/classes";

const thCell = "text-left py-1.5 pr-3 text-clevr-muted font-medium border-b border-clevr-border";
const tdCell = "py-1.5 pr-3 border-b border-clevr-border";
const tdCenter = "py-1.5 text-center border-b border-clevr-border";

export function ModulesTab() {
  const state = useAppState();
  const dispatch = useAppDispatch();

  function toggleModule(moduleName: string) {
    dispatch({ type: "TOGGLE_MODULE_EXCLUSION", moduleName });
  }

  function selectAllModules() {
    dispatch({ type: "SET_EXCLUDED_MODULES", modules: [] });
  }

  function deselectAllModules() {
    dispatch({ type: "SET_EXCLUDED_MODULES", modules: state.config.modules.map((m) => m.name) });
  }

  function excludeAllMarketplace() {
    dispatch({ type: "SET_MARKETPLACE_MODULES_EXCLUDED", exclude: true });
  }

  function includeAllMarketplace() {
    dispatch({ type: "SET_MARKETPLACE_MODULES_EXCLUDED", exclude: false });
  }

  return (
    <section className="mb-6">
      <h3 className={sectionHeading}>Modules</h3>
      <p className={settingsDesc}>
        Excluded modules are skipped on the next scan. Changes are written to{" "}
        <code>lint-config.yaml</code> in your project directory.
      </p>
      {state.config.modules.length === 0 ? (
        <p className={settingsEmpty}>No modules found — open a project in Studio Pro.</p>
      ) : (
        <table className={tableBase}>
          <thead>
            <tr>
              <th className={`${thCell} text-left`}>Module</th>
              <th className={`${thCell} w-[80px]`}>
                <div className="flex items-center gap-1">
                  <span>Marketplace</span>
                  {state.config.modules.some((m) => m.fromMarketplace) && (
                    <div className="flex items-center gap-1 ml-1 text-[11px]">
                      {state.config.modules.filter((m) => m.fromMarketplace).every((m) => state.config.excludedModules.pending.includes(m.name))
                        ? <button type="button" className={bulkBtn} onClick={includeAllMarketplace}>Include all</button>
                        : <button type="button" className={bulkBtn} onClick={excludeAllMarketplace}>Exclude all</button>
                      }
                    </div>
                  )}
                </div>
              </th>
              <th className={`${thCell} w-[110px]`}>
                <div className="flex items-center gap-1">
                  <span>Include in scan</span>
                  <div className="flex items-center gap-1 ml-1 text-[11px]">
                    <button type="button" className={bulkBtn} onClick={selectAllModules}>All</button>
                    <span className="text-clevr-muted">·</span>
                    <button type="button" className={bulkBtn} onClick={deselectAllModules}>None</button>
                  </div>
                </div>
              </th>
            </tr>
          </thead>
          <tbody>
            {state.config.modules.map((module) => {
              const isExcluded = state.config.excludedModules.pending.includes(module.name);
              return (
                <tr key={module.name} className={`cursor-pointer select-none hover:bg-clevr-hover ${isExcluded ? "opacity-50" : ""}`} onClick={() => toggleModule(module.name)}>
                  <td className={`${tdCell} font-medium`}>{module.name}</td>
                  <td className={`${tdCenter} w-[80px]`}>
                    {module.fromMarketplace && (
                      <span
                        className="inline-block px-[7px] py-px rounded-full text-[10px] font-bold tracking-[0.03em] bg-[#e7eef6] text-clevr-accent cursor-default select-none"
                        title={module.appStoreVersion ? `Mendix Marketplace v${module.appStoreVersion}` : "Mendix Marketplace"}
                      >
                        MX
                      </span>
                    )}
                  </td>
                  <td className={`${tdCenter} w-[110px]`}>
                    <input
                      type="checkbox"
                      checked={!isExcluded}
                      onChange={() => toggleModule(module.name)}
                      onClick={(e) => e.stopPropagation()}
                      title={isExcluded ? "Module is excluded — click to include" : "Module is included — click to exclude"}
                    />
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      )}
    </section>
  );
}
