// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;
using Microsoft.Quantum.Simulation.Core;

namespace Microsoft.Quantum.Simulation.Simulators
{
    public partial class QuantumSimulator
    {
        internal class QSimCheckQubitUniqueness : Intrinsic.CheckQubitUniqueness
        {
            private QuantumSimulator Simulator { get; }
            public QSimCheckQubitUniqueness(QuantumSimulator m) : base(m)
            {
                this.Simulator = m;
            }

            public override Func<IQArray<Qubit>, QVoid> __Body__ => (qubits) =>
            {
                Simulator.CheckQubits(qubits);
                return QVoid.Instance;
            };

            public override Func<(IQArray<Qubit>, IQArray<Qubit>), QVoid> __ControlledBody__ => (args) =>
            {
                var (ctrls, qubits) = args;
                Simulator.CheckQubits(QArray<Qubit>.Add(ctrls, qubits));
                return QVoid.Instance;
            };
        }

        internal class QSimRotationAngleValidation : Intrinsic.RotationAngleValidation
        {
            public QSimRotationAngleValidation(QuantumSimulator m) : base(m)
            {
            }

            public override Func<double, QVoid> __Body__ => (angle) =>
            {
                CheckAngle(angle);
                return QVoid.Instance;
            };
        }
    }
}
