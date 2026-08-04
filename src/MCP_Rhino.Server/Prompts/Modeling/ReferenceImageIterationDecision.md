# Reference Image Iteration Decision

Consume structured visual QA output and modeling trace data.

Choose one decision:

- revise massing when silhouette, proportion, scale, or part placement is wrong
- revise detail when recognizable geometry details are missing or inaccurate
- revise material when color, roughness, transparency, or texture is the main mismatch
- accept when no blocking QA findings remain
- stop and report gap when required tools are missing or iteration limits are reached

Do not perform image comparison in this skill. Use the visual QA tool output and explicit findings only.
