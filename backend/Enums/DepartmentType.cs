namespace Cia.Api.Enums;

public enum DepartmentType
{
    Triage = 1,
    TechnicalSupport = 2,
    ModemReplacement = 3,
    Financial = 4,
    HumanAgent = 5
}

public static class DepartmentNames
{
    public static string Format(DepartmentType department) => department switch
    {
        DepartmentType.Triage => "Triagem",
        DepartmentType.TechnicalSupport => "Suporte Técnico",
        DepartmentType.ModemReplacement => "Troca de Modem",
        DepartmentType.Financial => "Financeiro",
        DepartmentType.HumanAgent => "Atendimento Humano",
        _ => department.ToString()
    };
}
