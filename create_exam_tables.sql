-- Create Exam Tables Manually
-- Run this in SQL Server Management Studio or similar tool

-- Create Exams table
CREATE TABLE [Exams] (
    [Id] int NOT NULL IDENTITY,
    [Title] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NOT NULL,
    [ExamDate] datetime2 NOT NULL,
    [ExamSession] nvarchar(max) NOT NULL,
    [IsApproved] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [PartAMarks] int NOT NULL,
    [PartBMarks] int NOT NULL,
    [InternalMarks] int NOT NULL,
    [TotalMarks] int NOT NULL,
    [PassingPercentage] int NOT NULL,
    [PartATimeLimit] int NOT NULL,
    [PartBTimeLimit] int NOT NULL,
    [CourseId] int NOT NULL,
    [InstructorId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_Exams] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Exams_AspNetUsers_InstructorId] FOREIGN KEY ([InstructorId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Exams_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]) ON DELETE CASCADE
);

-- Create ExamQuestions table
CREATE TABLE [ExamQuestions] (
    [Id] int NOT NULL IDENTITY,
    [Text] nvarchar(1000) NOT NULL,
    [Type] int NOT NULL,
    [Part] int NOT NULL,
    [Points] int NOT NULL,
    [OrderIndex] int NOT NULL,
    [ExamId] int NOT NULL,
    CONSTRAINT [PK_ExamQuestions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ExamQuestions_Exams_ExamId] FOREIGN KEY ([ExamId]) REFERENCES [Exams] ([Id]) ON DELETE CASCADE
);

-- Create ExamSchedules table
CREATE TABLE [ExamSchedules] (
    [Id] int NOT NULL IDENTITY,
    [ScheduledDate] datetime2 NOT NULL,
    [Session] nvarchar(max) NOT NULL,
    [IsAssigned] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [ExamId] int NOT NULL,
    [StudentId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_ExamSchedules] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ExamSchedules_AspNetUsers_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ExamSchedules_Exams_ExamId] FOREIGN KEY ([ExamId]) REFERENCES [Exams] ([Id]) ON DELETE CASCADE
);

-- Create ExamQuestionOptions table
CREATE TABLE [ExamQuestionOptions] (
    [Id] int NOT NULL IDENTITY,
    [Text] nvarchar(500) NOT NULL,
    [IsCorrect] bit NOT NULL,
    [OrderIndex] int NOT NULL,
    [ExamQuestionId] int NOT NULL,
    CONSTRAINT [PK_ExamQuestionOptions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ExamQuestionOptions_ExamQuestions_ExamQuestionId] FOREIGN KEY ([ExamQuestionId]) REFERENCES [ExamQuestions] ([Id]) ON DELETE CASCADE
);

-- Create ExamAttempts table
CREATE TABLE [ExamAttempts] (
    [Id] int NOT NULL IDENTITY,
    [StartedAt] datetime2 NOT NULL,
    [CompletedAt] datetime2 NULL,
    [IsCompleted] bit NOT NULL,
    [PartAScore] int NOT NULL,
    [PartBScore] int NOT NULL,
    [InternalScore] int NOT NULL,
    [TotalScore] int NOT NULL,
    [Percentage] float NOT NULL,
    [IsPassed] bit NOT NULL,
    [PartACompleted] bit NOT NULL,
    [PartBCompleted] bit NOT NULL,
    [InternalAssigned] bit NOT NULL,
    [ResultPublished] bit NOT NULL,
    [ResultPublishedAt] datetime2 NULL,
    [ExamId] int NOT NULL,
    [StudentId] nvarchar(450) NOT NULL,
    [ExamScheduleId] int NULL,
    CONSTRAINT [PK_ExamAttempts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ExamAttempts_AspNetUsers_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ExamAttempts_ExamSchedules_ExamScheduleId] FOREIGN KEY ([ExamScheduleId]) REFERENCES [ExamSchedules] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ExamAttempts_Exams_ExamId] FOREIGN KEY ([ExamId]) REFERENCES [Exams] ([Id]) ON DELETE NO ACTION
);

-- Create ExamAnswers table
CREATE TABLE [ExamAnswers] (
    [Id] int NOT NULL IDENTITY,
    [AnswerText] nvarchar(max) NOT NULL,
    [IsCorrect] bit NOT NULL,
    [Points] int NOT NULL,
    [AnsweredAt] datetime2 NOT NULL,
    [ExamAttemptId] int NOT NULL,
    [ExamQuestionId] int NOT NULL,
    [SelectedOptionId] int NULL,
    CONSTRAINT [PK_ExamAnswers] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ExamAnswers_ExamAttempts_ExamAttemptId] FOREIGN KEY ([ExamAttemptId]) REFERENCES [ExamAttempts] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ExamAnswers_ExamQuestionOptions_SelectedOptionId] FOREIGN KEY ([SelectedOptionId]) REFERENCES [ExamQuestionOptions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ExamAnswers_ExamQuestions_ExamQuestionId] FOREIGN KEY ([ExamQuestionId]) REFERENCES [ExamQuestions] ([Id]) ON DELETE NO ACTION
);

-- Create ExamCertificates table
CREATE TABLE [ExamCertificates] (
    [Id] int NOT NULL IDENTITY,
    [CertificateNumber] nvarchar(max) NOT NULL,
    [IssuedAt] datetime2 NOT NULL,
    [ValidUntil] datetime2 NOT NULL,
    [FilePath] nvarchar(max) NOT NULL,
    [Percentage] float NOT NULL,
    [ExamAttemptId] int NOT NULL,
    [StudentId] nvarchar(450) NOT NULL,
    [CourseId] int NOT NULL,
    CONSTRAINT [PK_ExamCertificates] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ExamCertificates_AspNetUsers_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ExamCertificates_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ExamCertificates_ExamAttempts_ExamAttemptId] FOREIGN KEY ([ExamAttemptId]) REFERENCES [ExamAttempts] ([Id]) ON DELETE CASCADE
);

-- Create ReExamPayments table
CREATE TABLE [ReExamPayments] (
    [Id] int NOT NULL IDENTITY,
    [StudentId] nvarchar(450) NOT NULL,
    [ExamAttemptId] int NOT NULL,
    [ExamId] int NOT NULL,
    [CourseId] int NOT NULL,
    [StudentName] nvarchar(100) NOT NULL,
    [StudentEmail] nvarchar(max) NOT NULL,
    [ReExamFee] decimal(18,2) NOT NULL,
    [PaymentMethod] int NOT NULL,
    [TransactionId] nvarchar(50) NOT NULL,
    [PaymentStatus] nvarchar(20) NOT NULL,
    [PaymentDate] datetime2 NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_ReExamPayments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ReExamPayments_AspNetUsers_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ReExamPayments_ExamAttempts_ExamAttemptId] FOREIGN KEY ([ExamAttemptId]) REFERENCES [ExamAttempts] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ReExamPayments_Exams_ExamId] FOREIGN KEY ([ExamId]) REFERENCES [Exams] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ReExamPayments_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]) ON DELETE NO ACTION
);

-- Create indexes
CREATE INDEX [IX_ExamAnswers_ExamAttemptId] ON [ExamAnswers] ([ExamAttemptId]);
CREATE INDEX [IX_ExamAnswers_ExamQuestionId] ON [ExamAnswers] ([ExamQuestionId]);
CREATE INDEX [IX_ExamAnswers_SelectedOptionId] ON [ExamAnswers] ([SelectedOptionId]);
CREATE INDEX [IX_ExamAttempts_ExamId] ON [ExamAttempts] ([ExamId]);
CREATE INDEX [IX_ExamAttempts_ExamScheduleId] ON [ExamAttempts] ([ExamScheduleId]);
CREATE INDEX [IX_ExamAttempts_StudentId] ON [ExamAttempts] ([StudentId]);
CREATE UNIQUE INDEX [IX_ExamCertificates_ExamAttemptId] ON [ExamCertificates] ([ExamAttemptId]);
CREATE INDEX [IX_ExamCertificates_CourseId] ON [ExamCertificates] ([CourseId]);
CREATE INDEX [IX_ExamCertificates_StudentId] ON [ExamCertificates] ([StudentId]);
CREATE INDEX [IX_ExamQuestionOptions_ExamQuestionId] ON [ExamQuestionOptions] ([ExamQuestionId]);
CREATE INDEX [IX_ExamQuestions_ExamId] ON [ExamQuestions] ([ExamId]);
CREATE INDEX [IX_Exams_CourseId] ON [Exams] ([CourseId]);
CREATE INDEX [IX_Exams_InstructorId] ON [Exams] ([InstructorId]);
CREATE INDEX [IX_ExamSchedules_ExamId] ON [ExamSchedules] ([ExamId]);
CREATE INDEX [IX_ExamSchedules_StudentId] ON [ExamSchedules] ([StudentId]);
CREATE INDEX [IX_ReExamPayments_StudentId] ON [ReExamPayments] ([StudentId]);
CREATE INDEX [IX_ReExamPayments_ExamAttemptId] ON [ReExamPayments] ([ExamAttemptId]);
CREATE INDEX [IX_ReExamPayments_ExamId] ON [ReExamPayments] ([ExamId]);
CREATE INDEX [IX_ReExamPayments_CourseId] ON [ReExamPayments] ([CourseId]);

PRINT 'Exam system tables created successfully!';