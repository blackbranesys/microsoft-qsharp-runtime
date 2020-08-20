// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Quantum.Measurement {
    open Microsoft.Quantum.Intrinsic;

    internal operation BasisChangeZtoY(target : Qubit) : Unit is Adj + Ctl {
        H(target);
        S(target);
    }

    /// # Summary
    /// Measures a single qubit in the Y basis,
    /// and resets it to a fixed initial state
    /// following the measurement.
    ///
    /// # Description
    /// Performs a single-qubit measurement in the $Y$-basis,
    /// and ensures that the qubit is returned to $\ket{0}$
    /// following the measurement.
    ///
    /// # Input
    /// ## target
    /// A single qubit to be measured.
    ///
    /// # Output
    /// The result of measuring `target` in the Pauli $Y$ basis.
    operation MResetY (target : Qubit) : Result {
        let result = Measure([PauliY], [target]);

        // We must return the qubit to the Z basis as well.
        Adjoint BasisChangeZtoY(target);

        if (result == One) {
            // Recall that the +1 eigenspace of a measurement operator corresponds to
            // the Result case Zero. Thus, if we see a One case, we must reset the state
            // have +1 eigenvalue.
            X(target);
        }

        return result;
    }
}