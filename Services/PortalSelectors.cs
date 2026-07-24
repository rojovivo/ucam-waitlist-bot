namespace UcamWaitlistBot.Services;

/// <summary>
/// Centralised catalogue of the strings used to locate elements on the portal.
/// <para>
/// This is the single tuning surface. All values below were confirmed against the live
/// portal DOM (a Salesforce Lightning / LWC SPA). If the portal markup changes, adjust
/// these constants rather than the scraper logic.
/// </para>
/// </summary>
public static class PortalSelectors
{
    // --- Login page (standard Salesforce Identity login form) ---
    // Inputs live inside stable container IDs and are placeholder-driven (no reliable <label>),
    // so we target "container id + input". The submit button carries the visible text "Log in".
    public const string UsernameInput = "#sfdc_username_container input";
    public const string PasswordInput = "#sfdc_password_container input";
    public const string LoginButtonName = "Log in";

    // --- Dashboard ("En proceso de admisión") ---
    // The dashboard is built from LWC <div>s (no HTML table). The active in-process admission
    // exposes a single "Continuar" button; other listings use different actions. We click that
    // unique button and then verify the wizard opened for the configured program.
    public const string ContinueButtonName = "Continuar";

    // --- Wizard navigation ---
    // Step 4 of 6 is reached by clicking the "Consulta el resultado" step in the progress bar.
    public const string ResultStepText = "Consulta el resultado";

    // --- Results block (Paso 4 de 6, LWC <div> grid, not a <table>) ---
    // Two sibling grids: a header row of label cells and a value row of data cells, aligned by
    // column index. We find the index of the "Posición de espera" header and read the value cell
    // at the same index.
    public const string ResultHeaderCells = ".tab-group .column-element";
    public const string ResultValueCells = ".table-element .container-element";

    // Header text is mixed-case in the DOM ("Posición de espera") and uppercased via CSS, so we
    // match it with a case-insensitive regex. "." tolerates the accented "ó".
    public const string WaitlistHeaderPattern = "posici.n de espera";
}
