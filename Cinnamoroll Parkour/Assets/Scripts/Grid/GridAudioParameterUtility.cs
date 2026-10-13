using UnityEngine;

public static class GridAudioParameterUtility
{
    public static float GetPitchCentsForY(
        float designY,
        int minimumGridY,
        int[] scaleCents,
        float maximumPitchCents
    )
    {
        if (scaleCents == null || scaleCents.Length == 0)
        {
            return 0f;
        }

        float zeroBasedStep = Mathf.Max(0f, designY - minimumGridY);
        int lowerStep = Mathf.FloorToInt(zeroBasedStep);
        int upperStep = lowerStep + 1;
        float stepBlend = zeroBasedStep - lowerStep;

        float lowerPitch = GetPitchCentsForScaleStep(
            lowerStep,
            scaleCents
        );
        float upperPitch = GetPitchCentsForScaleStep(
            upperStep,
            scaleCents
        );
        float pitchCents = Mathf.Lerp(lowerPitch, upperPitch, stepBlend);

        return Mathf.Min(pitchCents, maximumPitchCents);
    }

    public static float GetSignedPanFromRange(
        float x,
        float minimumX,
        float maximumX,
        float responseCurve
    )
    {
        if (Mathf.Approximately(minimumX, maximumX))
        {
            return 0f;
        }

        float signedPan = Mathf.InverseLerp(
            minimumX,
            maximumX,
            x
        ) * 2f - 1f;

        return ApplyPanResponseCurve(signedPan, responseCurve);
    }

    public static float ApplyPanResponseCurve(
        float signedPan,
        float responseCurve
    )
    {
        float curve = Mathf.Max(0.01f, responseCurve);
        float sign = Mathf.Sign(signedPan);
        float magnitude = Mathf.Pow(Mathf.Abs(signedPan), curve);

        return sign * magnitude;
    }

    private static float GetPitchCentsForScaleStep(
        int step,
        int[] scaleCents
    )
    {
        int octave = step / scaleCents.Length;
        int scaleIndex = step % scaleCents.Length;

        return octave * 1200f + scaleCents[scaleIndex];
    }
}
