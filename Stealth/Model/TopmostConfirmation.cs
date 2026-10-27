using System;

namespace Stealth.Model
{
    internal enum TopmostConfirmationResult
    {
        Confirmed,
        RetryIssued,
        Failed
    }

    // A bounded two-stage confirmation for one explicit user action.
    // The second attempt is verified on a LATER dispatcher tick.
    internal sealed class TopmostConfirmation
    {
        private readonly bool expected;
        private bool retryIssued;

        internal TopmostConfirmation(bool expected)
        {
            this.expected = expected;
        }

        internal TopmostConfirmationResult Observe(bool actual, Action retry)
        {
            if (actual == expected) return TopmostConfirmationResult.Confirmed;
            if (retryIssued) return TopmostConfirmationResult.Failed;
            retryIssued = true;
            retry();
            return TopmostConfirmationResult.RetryIssued;
        }
    }
}
