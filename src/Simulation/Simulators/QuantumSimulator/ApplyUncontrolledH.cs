﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;
using Microsoft.Quantum.Simulation.Core;

namespace Microsoft.Quantum.Simulation.Simulators
{
    public partial class QuantumSimulator
    {
        public class QSimApplyUncontrolledH : Intrinsic.ApplyUncontrolledH
        {
            [DllImport(QSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "H")]
            private static extern void H(uint id, uint qubit);

            private QuantumSimulator Simulator { get; }


            public QSimApplyUncontrolledH(QuantumSimulator m) : base(m)
            {
                this.Simulator = m;
            }

            public override Func<Qubit, QVoid> __Body__ => (q1) =>
            {
                Simulator.CheckQubit(q1);

                H(Simulator.Id, (uint)q1.Id);

                return QVoid.Instance;
            };
        }
    }
}
