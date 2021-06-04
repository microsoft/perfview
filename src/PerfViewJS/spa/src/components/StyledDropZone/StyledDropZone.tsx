import React, { useMemo } from "react";
import { DropEvent, FileRejection, useDropzone } from "react-dropzone";
import { Stack, Text, getTheme, IStackTokens } from "@fluentui/react";
import { Col, Container, Row } from "react-grid-system";
const theme = getTheme();
const baseStyle = {
  borderWidth: 2,
  borderRadius: 2,
  borderStyle: "dashed",
  borderColor: theme.semanticColors.primaryButtonBackground,
  outline: "none",
  transition: "border .24s ease-in-out",
};

const activeStyle = {
  borderColor: theme.semanticColors.severeWarningBackground,
};

const acceptStyle = {
  borderColor: "#00e676",
};

const rejectStyle = {
  borderColor: "#ff1744",
};

const itemAlignmentsStackTokens: IStackTokens = {
  padding: 70,
};

export interface IStyledDropzone {
  onDrop: <T extends File>(acceptedFiles: T[], fileRejections: FileRejection[], event: DropEvent) => void;
}

const StyledDropzone = (props: IStyledDropzone) => {
  const { onDrop } = props;
  const { getRootProps, getInputProps, isDragActive, isDragAccept, isDragReject } = useDropzone({ onDrop: onDrop });

  const style = useMemo(
    () => ({
      ...baseStyle,
      ...(isDragActive ? activeStyle : {}),
      ...(isDragAccept ? acceptStyle : {}),
      ...(isDragReject ? rejectStyle : {}),
    }),
    [isDragActive, isDragReject, isDragAccept]
  );

  return (
    <Container style={{ width: "100%", paddingTop: "10px" }}>
      <Row align="center">
        <Col>
          <div {...getRootProps({ style })}>
            <input {...getInputProps()} />
            <Stack>
              <Stack.Item align="center" tokens={itemAlignmentsStackTokens}>
                <Text style={{ opacity: 0.4 }} variant={"xLarge"}>
                  Drag and drop some files here, or click to select files
                </Text>
              </Stack.Item>
            </Stack>
          </div>
        </Col>
      </Row>
    </Container>
  );
};

export default StyledDropzone;
