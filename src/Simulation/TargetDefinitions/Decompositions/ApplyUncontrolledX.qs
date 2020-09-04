// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Quantum.Intrinsic {
    open Microsoft.Quantum.Diagnostics;

    /// # Summary
    /// Applies the Pauli $X$ gate. Note that the Controlled functor is not supported.
    ///
    /// # Description
    /// \begin{align}
    ///     \sigma_x \mathrel{:=}
    ///     \begin{bmatrix}
    ///         0 & 1 \\\\
    ///         1 & 0
    ///     \end{bmatrix}.
    /// \end{align}
    ///
    /// # Input
    /// ## qubit
    /// Qubit to which the gate should be applied.
    @EnableTestingViaName("Test.TargetDefinitions.ApplyUncontrolledX")
    operation ApplyUncontrolledX (qubit : Qubit) : Unit is Adj {
        X(qubit);
    }
}