namespace Authorization.Core;

/// <summary>Why a request was refused. <see cref="None"/> means it was not.</summary>
/// <remarks>
/// <b>A code as well as a sentence.</b> Tests assert the code, so the wording stays free to improve
/// without breaking them — and, more importantly, so an assertion about <i>which</i> refusal
/// occurred cannot be quietly weakened into one that passes for any refusal.
/// </remarks>
public enum DenialReason
{
    /// <summary>Not a refusal.</summary>
    None = 0,

    /// <summary>The subject does not hold the permission the operation needs.</summary>
    NotPermitted,

    /// <summary>The subject raised this record, and may not approve their own.</summary>
    OwnRequisition,

    /// <summary>The amount is beyond what this subject may approve.</summary>
    AboveApprovalLimit,

    /// <summary>The record is not in a state where the operation means anything.</summary>
    WrongState,

    /// <summary>The operation belongs to whoever raised the record, and that is somebody else.</summary>
    NotRaiser,
}

/// <summary>
/// Whether the interface should offer an operation, and what to tell the user if not.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is not a security decision.</b> It decides what a screen offers. The server decides what
/// actually happens, and it is the only decision anybody is obliged to respect.
/// </para>
/// <para>
/// <b>A refusal carries an explanation</b> because a control that is grey for no stated reason is
/// indistinguishable from a control that is broken.
/// </para>
/// </remarks>
public sealed record AuthorizationDecision(bool IsAllowed, DenialReason Reason, string Explanation)
{
    /// <summary>The interface may offer this.</summary>
    public static AuthorizationDecision Allowed { get; } = new(true, DenialReason.None, string.Empty);

    /// <summary>The interface should not offer this, and should say why.</summary>
    public static AuthorizationDecision Denied(DenialReason reason, string explanation) =>
        new(false, reason, explanation);
}
