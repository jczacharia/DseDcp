namespace Dse.Core;

// Temporary probe to validate scripts/sonar.cs gate prediction — delete me.
public static class PreflightSmellProbe
{
    public static string Classify(int value)
    {
        var result = "";
        if (value > 0)
        {
            if (value > 10)
            {
                if (value > 100)
                {
                    result = value > 1000 ? "huge" : "large";
                }
                else
                {
                    result = "medium";
                }
            }
            else
            {
                result = "small";
            }
        }
        else
        {
            try
            {
                result = (100 / value).ToString();
            }
            catch (Exception)
            {
                // swallowed
            }
        }
        return result;
    }
}
