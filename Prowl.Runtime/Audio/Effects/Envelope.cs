// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

// Originally Created by Nigel Redmon on 12/18/12.
// EarLevel Engineering: earlevel.com
// C# Port 2024 W.M.R Jap-A-Joe

using System;
using Prowl.Runtime.Audio;

namespace Prowl.Runtime.Audio.Effects
{
    public class ADSR
    {
        public double a;
        public double d;
        public double s;
        public double r;

        public ADSR(double a, double d, double s, double r)
        {
            this.a = a * AudioContext.SampleRate;
            this.d = d * AudioContext.SampleRate;
            this.s = s * AudioContext.SampleRate;
            this.r = r * AudioContext.SampleRate;
        }
    }

    public sealed class Envelope
    {
        public enum EnvelopeState
        {
            Idle = 0,
            Attack,
            Decay,
            Sustain,
            Release
        }

        private EnvelopeState state;
        private double output;
        private double attackRate;
        private double decayRate;
        private double releaseRate;
        private double attackCoef;
        private double decayCoef;
        private double releaseCoef;
        private double sustainLevel;
        private double targetRatioA;
        private double targetRatioDR;
        private double attackBase;
        private double decayBase;
        private double releaseBase;

        public double Attack
        {
            get
            {
                return attackRate;
            }
            set
            {
                SetAttackRate(value);
            }
        }

        public double Decay
        {
            get
            {
                return decayRate;
            }
            set
            {
                SetDecayRate(value);
            }
        }

        public double Sustain
        {
            get
            {
                return sustainLevel;
            }
            set
            {
                SetSustainLevel(value);
            }
        }

        public double Release
        {
            get
            {
                return releaseRate;
            }
            set
            {
                SetReleaseRate(value);
            }
        }

        public EnvelopeState State
        {
            get
            {
                return state;
            }
        }

        public double Output
        {
            get
            {
                return output;
            }
        }

        public Envelope()
        {
            Reset();

            SetAttackRate(0);
            SetDecayRate(0);
            SetReleaseRate(0);
            SetSustainLevel(1.0);
            SetTargetRatioA(0.3f);
            SetTargetRatioDR(0.0001);

            state = EnvelopeState.Attack;
        }

        public Envelope(ADSR config)
        {
            Reset();

            SetAttackRate(config.a);
            SetDecayRate(config.d);
            SetReleaseRate(config.r);
            SetSustainLevel(config.s);
            SetTargetRatioA(0.3f);
            SetTargetRatioDR(0.0001);

            state = EnvelopeState.Attack;
        }

        public Envelope(double attackRate, double decayRate, double sustainLevel, double releaseRate)
        {
            Reset();

            SetAttackRate(attackRate);
            SetDecayRate(decayRate);
            SetReleaseRate(releaseRate);
            SetSustainLevel(sustainLevel);
            SetTargetRatioA(0.3f);
            SetTargetRatioDR(0.0001);

            state = EnvelopeState.Attack;
        }

        public float Process()
        {
            switch (state) 
            {
                case EnvelopeState.Idle:
                {
                    break;
                }
                case EnvelopeState.Attack:
                {
                    output = attackBase + output * attackCoef;
                    if (output >= 1.0) {
                        output = 1.0;
                        state = EnvelopeState.Decay;
                    }
                    break;
                }
                case EnvelopeState.Decay:
                {
                    output = decayBase + output * decayCoef;
                    if (output <= sustainLevel) {
                        output = sustainLevel;
                        state = EnvelopeState.Sustain;
                    }
                    break;
                }
                case EnvelopeState.Sustain:
                {
                    break;
                }
                case EnvelopeState.Release:
                {
                    output = releaseBase + output * releaseCoef;
                    if (output <= 0.0) {
                        output = 0.0;
                        state = EnvelopeState.Idle;
                    }
                    break;
                }
            }
            return (float)output;
        }

        public void SetGate(bool enabled)
        {
            if (enabled)
                state = EnvelopeState.Attack;
            else if (state != EnvelopeState.Idle)
                state = EnvelopeState.Release;
        }

        public void Reset()
        {
            state = EnvelopeState.Idle;
            output = 0.0;
        }

        private void SetAttackRate(double rate)
        {
            attackRate = rate;
            attackCoef = CalcCoef(rate, targetRatioA);
            attackBase = (1.0 + targetRatioA) * (1.0 - attackCoef);
        }

        private void SetDecayRate(double rate)
        {
            decayRate = rate;
            decayCoef = CalcCoef(rate, targetRatioDR);
            decayBase = (sustainLevel - targetRatioDR) * (1.0 - decayCoef);
        }

        private void SetReleaseRate(double rate)
        {
            releaseRate = rate;
            releaseCoef = CalcCoef(rate, targetRatioDR);
            releaseBase = -targetRatioDR * (1.0 - releaseCoef);
        }

        private double CalcCoef(double rate, double targetRatio)
        {
            return (rate <= 0) ? 0.0 : Math.Exp(-Math.Log((1.0 + targetRatio) / targetRatio) / rate);
        }

        private void SetSustainLevel(double level)
        {
            sustainLevel = level;
            decayBase = (sustainLevel - targetRatioDR) * (1.0 - decayCoef);
        }

        private void SetTargetRatioA(double targetRatio)
        {
            if (targetRatio < 0.000000001)
                targetRatio = 0.000000001;  // -180 dB
            targetRatioA = targetRatio;
            attackCoef = CalcCoef(attackRate, targetRatioA);
            attackBase = (1.0 + targetRatioA) * (1.0 - attackCoef);
        }

        private void SetTargetRatioDR(double targetRatio)
        {
            if (targetRatio < 0.000000001)
                targetRatio = 0.000000001;  // -180 dB
            targetRatioDR = targetRatio;
            decayCoef = CalcCoef(decayRate, targetRatioDR);
            releaseCoef = CalcCoef(releaseRate, targetRatioDR);
            decayBase = (sustainLevel - targetRatioDR) * (1.0 - decayCoef);
            releaseBase = -targetRatioDR * (1.0 - releaseCoef);
        }
    }
}
