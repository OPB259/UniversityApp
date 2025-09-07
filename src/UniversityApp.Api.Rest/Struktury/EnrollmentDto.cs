namespace UniversityApp.Api.Rest;
public record EnrollmentDto(
    int Id,
    int StudentId,
    string StudentName,
    int CourseId,
    string CourseTitle,
    DateTime EnrolledAt);
